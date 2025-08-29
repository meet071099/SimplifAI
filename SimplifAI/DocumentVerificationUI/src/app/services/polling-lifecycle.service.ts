import { Injectable, OnDestroy } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, fromEvent, merge } from 'rxjs';
import { takeUntil, filter } from 'rxjs/operators';
import { PollingStateService } from './polling-state.service';

export interface ComponentPollingContext {
  componentId: string;
  documentIds: string[];
  route: string;
  registrationTime: number;
}

@Injectable({
  providedIn: 'root'
})
export class PollingLifecycleService implements OnDestroy {
  private destroy$ = new Subject<void>();
  private componentContexts = new Map<string, ComponentPollingContext>();
  private routePollingMap = new Map<string, Set<string>>(); // route -> document IDs

  constructor(
    private pollingStateService: PollingStateService,
    private router: Router
  ) {
    this.initializeLifecycleManagement();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.cleanupAllPolling();
  }

  /**
   * Initialize lifecycle management for polling cleanup
   */
  private initializeLifecycleManagement(): void {
    // Handle page refresh and browser close
    this.setupPageUnloadHandling();
    
    // Handle route navigation
    this.setupRouteChangeHandling();
    
    // Handle visibility changes (tab switching)
    this.setupVisibilityChangeHandling();

    console.log('PollingLifecycleService: Initialized lifecycle management');
  }

  /**
   * Setup page unload handling for cleanup
   */
  private setupPageUnloadHandling(): void {
    const beforeUnload$ = fromEvent(window, 'beforeunload');
    const unload$ = fromEvent(window, 'unload');
    const pagehide$ = fromEvent(window, 'pagehide');

    merge(beforeUnload$, unload$, pagehide$)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        console.log('PollingLifecycleService: Page unload detected, cleaning up polling');
        this.cleanupAllPolling();
      });
  }

  /**
   * Setup route change handling for cleanup
   */
  private setupRouteChangeHandling(): void {
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe((event) => {
        const navigationEnd = event as NavigationEnd;
        console.log('PollingLifecycleService: Route change detected:', navigationEnd.url);
        this.handleRouteChange(navigationEnd.url);
      });
  }

  /**
   * Setup visibility change handling
   */
  private setupVisibilityChangeHandling(): void {
    fromEvent(document, 'visibilitychange')
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (document.hidden) {
          console.log('PollingLifecycleService: Page hidden, pausing aggressive polling');
          this.pauseAggressivePolling();
        } else {
          console.log('PollingLifecycleService: Page visible, resuming normal polling');
          this.resumeNormalPolling();
        }
      });
  }

  /**
   * Register a component for polling lifecycle management
   */
  public registerComponent(
    componentId: string,
    documentIds: string[],
    route?: string
  ): void {
    const currentRoute = route || this.router.url;
    
    const context: ComponentPollingContext = {
      componentId,
      documentIds: [...documentIds],
      route: currentRoute,
      registrationTime: Date.now()
    };

    this.componentContexts.set(componentId, context);

    // Track route-based polling
    if (!this.routePollingMap.has(currentRoute)) {
      this.routePollingMap.set(currentRoute, new Set());
    }
    documentIds.forEach(docId => {
      this.routePollingMap.get(currentRoute)!.add(docId);
    });

    console.log(`PollingLifecycleService: Registered component ${componentId} for polling:`, {
      documentIds,
      route: currentRoute,
      totalComponents: this.componentContexts.size
    });
  }

  /**
   * Unregister a component and cleanup its polling
   */
  public unregisterComponent(componentId: string): void {
    const context = this.componentContexts.get(componentId);
    if (!context) {
      console.warn(`PollingLifecycleService: Cannot unregister unknown component ${componentId}`);
      return;
    }

    // Cleanup polling for this component's documents
    context.documentIds.forEach(documentId => {
      this.pollingStateService.cleanupPollingResources(documentId);
    });

    // Remove from route tracking
    const routeDocuments = this.routePollingMap.get(context.route);
    if (routeDocuments) {
      context.documentIds.forEach(docId => {
        routeDocuments.delete(docId);
      });
      
      // Remove route entry if no documents left
      if (routeDocuments.size === 0) {
        this.routePollingMap.delete(context.route);
      }
    }

    this.componentContexts.delete(componentId);

    console.log(`PollingLifecycleService: Unregistered component ${componentId}:`, {
      cleanedDocuments: context.documentIds,
      remainingComponents: this.componentContexts.size
    });
  }

  /**
   * Add document to component's polling context
   */
  public addDocumentToComponent(componentId: string, documentId: string): void {
    const context = this.componentContexts.get(componentId);
    if (!context) {
      console.warn(`PollingLifecycleService: Cannot add document to unknown component ${componentId}`);
      return;
    }

    if (!context.documentIds.includes(documentId)) {
      context.documentIds.push(documentId);
      
      // Add to route tracking
      const routeDocuments = this.routePollingMap.get(context.route);
      if (routeDocuments) {
        routeDocuments.add(documentId);
      }

      console.log(`PollingLifecycleService: Added document ${documentId} to component ${componentId}`);
    }
  }

  /**
   * Remove document from component's polling context
   */
  public removeDocumentFromComponent(componentId: string, documentId: string): void {
    const context = this.componentContexts.get(componentId);
    if (!context) {
      console.warn(`PollingLifecycleService: Cannot remove document from unknown component ${componentId}`);
      return;
    }

    const index = context.documentIds.indexOf(documentId);
    if (index > -1) {
      context.documentIds.splice(index, 1);
      
      // Remove from route tracking
      const routeDocuments = this.routePollingMap.get(context.route);
      if (routeDocuments) {
        routeDocuments.delete(documentId);
      }

      // Cleanup polling for this document
      this.pollingStateService.cleanupPollingResources(documentId);

      console.log(`PollingLifecycleService: Removed document ${documentId} from component ${componentId}`);
    }
  }

  /**
   * Handle route changes and cleanup polling for old routes
   */
  private handleRouteChange(newRoute: string): void {
    const currentRoute = this.router.url;
    
    // Find components that are no longer on the current route
    const componentsToCleanup: string[] = [];
    
    this.componentContexts.forEach((context, componentId) => {
      if (context.route !== newRoute && context.route !== currentRoute) {
        componentsToCleanup.push(componentId);
      }
    });

    // Cleanup components from old routes
    componentsToCleanup.forEach(componentId => {
      console.log(`PollingLifecycleService: Cleaning up component ${componentId} due to route change`);
      this.unregisterComponent(componentId);
    });

    // Cleanup polling for documents that are no longer tracked by any route
    this.cleanupOrphanedPolling();
  }

  /**
   * Cleanup polling sessions that are no longer tracked by any component
   */
  private cleanupOrphanedPolling(): void {
    const trackedDocuments = new Set<string>();
    
    // Collect all tracked document IDs
    this.componentContexts.forEach((context) => {
      context.documentIds.forEach(docId => trackedDocuments.add(docId));
    });

    // Find active polling sessions that are not tracked
    const activePollingDocuments = this.pollingStateService.getActivePollingDocuments();
    const orphanedDocuments = activePollingDocuments.filter(docId => !trackedDocuments.has(docId));

    // Cleanup orphaned polling
    orphanedDocuments.forEach(documentId => {
      console.log(`PollingLifecycleService: Cleaning up orphaned polling for document ${documentId}`);
      this.pollingStateService.cleanupPollingResources(documentId);
    });

    if (orphanedDocuments.length > 0) {
      console.log(`PollingLifecycleService: Cleaned up ${orphanedDocuments.length} orphaned polling sessions`);
    }
  }

  /**
   * Pause aggressive polling when page is hidden
   */
  private pauseAggressivePolling(): void {
    // This could be implemented to reduce polling frequency when page is hidden
    // For now, we'll just log the event
    console.log('PollingLifecycleService: Pausing aggressive polling (page hidden)');
  }

  /**
   * Resume normal polling when page becomes visible
   */
  private resumeNormalPolling(): void {
    // This could be implemented to restore normal polling frequency
    // For now, we'll just log the event
    console.log('PollingLifecycleService: Resuming normal polling (page visible)');
  }

  /**
   * Cleanup all polling and component contexts
   */
  public cleanupAllPolling(): void {
    console.log(`PollingLifecycleService: Cleaning up all polling (${this.componentContexts.size} components)`);

    // Cleanup all polling sessions
    this.pollingStateService.cleanupAllPollingResources();

    // Clear all component contexts
    this.componentContexts.clear();
    this.routePollingMap.clear();

    console.log('PollingLifecycleService: All polling cleanup completed');
  }

  /**
   * Get component polling context
   */
  public getComponentContext(componentId: string): ComponentPollingContext | undefined {
    return this.componentContexts.get(componentId);
  }

  /**
   * Get all registered components
   */
  public getRegisteredComponents(): ComponentPollingContext[] {
    return Array.from(this.componentContexts.values());
  }

  /**
   * Get polling documents for current route
   */
  public getPollingDocumentsForRoute(route?: string): string[] {
    const targetRoute = route || this.router.url;
    const routeDocuments = this.routePollingMap.get(targetRoute);
    return routeDocuments ? Array.from(routeDocuments) : [];
  }

  /**
   * Check if component is registered
   */
  public isComponentRegistered(componentId: string): boolean {
    return this.componentContexts.has(componentId);
  }

  /**
   * Get lifecycle statistics
   */
  public getLifecycleStatistics(): {
    totalComponents: number;
    totalTrackedDocuments: number;
    routesWithPolling: number;
    averageComponentAge: number;
    oldestComponent?: {
      componentId: string;
      age: number;
      documentCount: number;
    };
  } {
    const components = Array.from(this.componentContexts.values());
    const now = Date.now();
    
    const totalTrackedDocuments = components.reduce((sum, ctx) => sum + ctx.documentIds.length, 0);
    const averageAge = components.length > 0
      ? components.reduce((sum, ctx) => sum + (now - ctx.registrationTime), 0) / components.length
      : 0;

    let oldestComponent: { componentId: string; age: number; documentCount: number; } | undefined;
    if (components.length > 0) {
      const oldest = components.reduce((oldest, ctx) => 
        ctx.registrationTime < oldest.registrationTime ? ctx : oldest
      );
      oldestComponent = {
        componentId: oldest.componentId,
        age: now - oldest.registrationTime,
        documentCount: oldest.documentIds.length
      };
    }

    return {
      totalComponents: components.length,
      totalTrackedDocuments,
      routesWithPolling: this.routePollingMap.size,
      averageComponentAge: Math.round(averageAge),
      oldestComponent
    };
  }

  /**
   * Force cleanup of stale components (older than specified age)
   */
  public cleanupStaleComponents(maxAgeMs: number = 300000): number { // 5 minutes default
    const now = Date.now();
    const staleComponents: string[] = [];

    this.componentContexts.forEach((context, componentId) => {
      const age = now - context.registrationTime;
      if (age > maxAgeMs) {
        staleComponents.push(componentId);
      }
    });

    staleComponents.forEach(componentId => {
      console.log(`PollingLifecycleService: Cleaning up stale component ${componentId}`);
      this.unregisterComponent(componentId);
    });

    if (staleComponents.length > 0) {
      console.log(`PollingLifecycleService: Cleaned up ${staleComponents.length} stale components`);
    }

    return staleComponents.length;
  }
}