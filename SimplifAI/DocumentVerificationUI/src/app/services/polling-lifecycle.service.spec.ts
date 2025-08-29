import { TestBed } from '@angular/core/testing';
import { Router, NavigationEnd } from '@angular/router';
import { Subject, of } from 'rxjs';
import { PollingLifecycleService, ComponentPollingContext } from './polling-lifecycle.service';
import { PollingStateService } from './polling-state.service';

describe('PollingLifecycleService', () => {
  let service: PollingLifecycleService;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockPollingStateService: jasmine.SpyObj<PollingStateService>;
  let routerEventsSubject: Subject<any>;

  beforeEach(() => {
    routerEventsSubject = new Subject();
    
    const routerSpy = jasmine.createSpyObj('Router', ['navigate'], {
      events: routerEventsSubject.asObservable(),
      url: '/test-route'
    });

    const pollingStateServiceSpy = jasmine.createSpyObj('PollingStateService', [
      'cleanupPollingResources',
      'cleanupAllPollingResources',
      'getActivePollingDocuments'
    ]);

    TestBed.configureTestingModule({
      providers: [
        PollingLifecycleService,
        { provide: Router, useValue: routerSpy },
        { provide: PollingStateService, useValue: pollingStateServiceSpy }
      ]
    });

    service = TestBed.inject(PollingLifecycleService);
    mockRouter = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    mockPollingStateService = TestBed.inject(PollingStateService) as jasmine.SpyObj<PollingStateService>;
  });

  afterEach(() => {
    service.cleanupAllPolling();
    routerEventsSubject.complete();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('component registration', () => {
    it('should register a component for polling lifecycle management', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1', 'doc-2'];
      const route = '/upload';

      service.registerComponent(componentId, documentIds, route);

      expect(service.isComponentRegistered(componentId)).toBe(true);
      
      const context = service.getComponentContext(componentId);
      expect(context).toBeTruthy();
      expect(context!.componentId).toBe(componentId);
      expect(context!.documentIds).toEqual(documentIds);
      expect(context!.route).toBe(route);
    });

    it('should use current router URL when no route is provided', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1'];
      
      Object.defineProperty(mockRouter, 'url', { value: '/current-route', configurable: true });
      service.registerComponent(componentId, documentIds);

      const context = service.getComponentContext(componentId);
      expect(context!.route).toBe('/current-route');
    });

    it('should track polling documents for routes', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1', 'doc-2'];
      const route = '/upload';

      service.registerComponent(componentId, documentIds, route);

      const routeDocuments = service.getPollingDocumentsForRoute(route);
      expect(routeDocuments).toEqual(jasmine.arrayContaining(documentIds));
    });
  });

  describe('component unregistration', () => {
    it('should unregister a component and cleanup its polling', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1', 'doc-2'];

      service.registerComponent(componentId, documentIds);
      service.unregisterComponent(componentId);

      expect(service.isComponentRegistered(componentId)).toBe(false);
      expect(mockPollingStateService.cleanupPollingResources).toHaveBeenCalledWith('doc-1');
      expect(mockPollingStateService.cleanupPollingResources).toHaveBeenCalledWith('doc-2');
    });

    it('should handle unregistering unknown component gracefully', () => {
      spyOn(console, 'warn');
      
      service.unregisterComponent('unknown-component');
      
      expect(console.warn).toHaveBeenCalledWith(
        'PollingLifecycleService: Cannot unregister unknown component unknown-component'
      );
    });

    it('should remove route tracking when unregistering component', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1'];
      const route = '/upload';

      service.registerComponent(componentId, documentIds, route);
      expect(service.getPollingDocumentsForRoute(route)).toEqual(['doc-1']);

      service.unregisterComponent(componentId);
      expect(service.getPollingDocumentsForRoute(route)).toEqual([]);
    });
  });

  describe('document management', () => {
    it('should add document to component context', () => {
      const componentId = 'test-component-1';
      const initialDocs = ['doc-1'];
      const newDoc = 'doc-2';

      service.registerComponent(componentId, initialDocs);
      service.addDocumentToComponent(componentId, newDoc);

      const context = service.getComponentContext(componentId);
      expect(context!.documentIds).toEqual(['doc-1', 'doc-2']);
    });

    it('should not add duplicate documents', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1'];

      service.registerComponent(componentId, documentIds);
      service.addDocumentToComponent(componentId, 'doc-1'); // Duplicate

      const context = service.getComponentContext(componentId);
      expect(context!.documentIds).toEqual(['doc-1']);
    });

    it('should remove document from component context', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1', 'doc-2'];

      service.registerComponent(componentId, documentIds);
      service.removeDocumentFromComponent(componentId, 'doc-1');

      const context = service.getComponentContext(componentId);
      expect(context!.documentIds).toEqual(['doc-2']);
      expect(mockPollingStateService.cleanupPollingResources).toHaveBeenCalledWith('doc-1');
    });

    it('should handle adding document to unknown component', () => {
      spyOn(console, 'warn');
      
      service.addDocumentToComponent('unknown-component', 'doc-1');
      
      expect(console.warn).toHaveBeenCalledWith(
        'PollingLifecycleService: Cannot add document to unknown component unknown-component'
      );
    });

    it('should handle removing document from unknown component', () => {
      spyOn(console, 'warn');
      
      service.removeDocumentFromComponent('unknown-component', 'doc-1');
      
      expect(console.warn).toHaveBeenCalledWith(
        'PollingLifecycleService: Cannot remove document from unknown component unknown-component'
      );
    });
  });

  describe('route change handling', () => {
    it('should cleanup components when route changes', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1'];
      const oldRoute = '/old-route';

      // Register component with old route
      service.registerComponent(componentId, documentIds, oldRoute);
      expect(service.isComponentRegistered(componentId)).toBe(true);

      // Simulate route change
      Object.defineProperty(mockRouter, 'url', { value: '/new-route', configurable: true });
      routerEventsSubject.next(new NavigationEnd(1, '/new-route', '/new-route'));

      // Component should be cleaned up since it's not on the current route
      expect(service.isComponentRegistered(componentId)).toBe(false);
    });

    it('should cleanup orphaned polling sessions', () => {
      const componentId = 'test-component-1';
      const documentIds = ['doc-1'];

      service.registerComponent(componentId, documentIds);
      
      // Mock active polling documents that include orphaned ones
      mockPollingStateService.getActivePollingDocuments.and.returnValue(['doc-1', 'orphaned-doc']);

      // Simulate route change
      routerEventsSubject.next(new NavigationEnd(1, '/new-route', '/new-route'));

      // Should cleanup orphaned document
      expect(mockPollingStateService.cleanupPollingResources).toHaveBeenCalledWith('orphaned-doc');
    });
  });

  describe('lifecycle statistics', () => {
    it('should calculate lifecycle statistics correctly', () => {
      const component1 = 'comp-1';
      const component2 = 'comp-2';
      
      // Register components at different times
      service.registerComponent(component1, ['doc-1', 'doc-2']);
      
      // Wait a bit and register second component
      setTimeout(() => {
        service.registerComponent(component2, ['doc-3']);
        
        const stats = service.getLifecycleStatistics();
        
        expect(stats.totalComponents).toBe(2);
        expect(stats.totalTrackedDocuments).toBe(3);
        expect(stats.routesWithPolling).toBe(1); // Both on same route
        expect(stats.averageComponentAge).toBeGreaterThan(0);
        expect(stats.oldestComponent).toBeTruthy();
        expect(stats.oldestComponent!.componentId).toBe(component1);
      }, 10);
    });

    it('should handle empty statistics', () => {
      const stats = service.getLifecycleStatistics();
      
      expect(stats.totalComponents).toBe(0);
      expect(stats.totalTrackedDocuments).toBe(0);
      expect(stats.routesWithPolling).toBe(0);
      expect(stats.averageComponentAge).toBe(0);
      expect(stats.oldestComponent).toBeUndefined();
    });
  });

  describe('stale component cleanup', () => {
    it('should cleanup stale components', (done) => {
      const componentId = 'stale-component';
      const documentIds = ['doc-1'];
      const maxAge = 100; // 100ms

      service.registerComponent(componentId, documentIds);
      expect(service.isComponentRegistered(componentId)).toBe(true);

      // Wait for component to become stale
      setTimeout(() => {
        const cleanedCount = service.cleanupStaleComponents(maxAge);
        
        expect(cleanedCount).toBe(1);
        expect(service.isComponentRegistered(componentId)).toBe(false);
        done();
      }, maxAge + 10);
    });

    it('should not cleanup fresh components', () => {
      const componentId = 'fresh-component';
      const documentIds = ['doc-1'];
      const maxAge = 1000; // 1 second

      service.registerComponent(componentId, documentIds);
      
      const cleanedCount = service.cleanupStaleComponents(maxAge);
      
      expect(cleanedCount).toBe(0);
      expect(service.isComponentRegistered(componentId)).toBe(true);
    });
  });

  describe('cleanup operations', () => {
    it('should cleanup all polling and component contexts', () => {
      const component1 = 'comp-1';
      const component2 = 'comp-2';
      
      service.registerComponent(component1, ['doc-1']);
      service.registerComponent(component2, ['doc-2']);
      
      expect(service.getRegisteredComponents().length).toBe(2);
      
      service.cleanupAllPolling();
      
      expect(service.getRegisteredComponents().length).toBe(0);
      expect(mockPollingStateService.cleanupAllPollingResources).toHaveBeenCalled();
    });
  });

  describe('utility methods', () => {
    it('should get registered components', () => {
      const component1 = 'comp-1';
      const component2 = 'comp-2';
      
      service.registerComponent(component1, ['doc-1']);
      service.registerComponent(component2, ['doc-2']);
      
      const components = service.getRegisteredComponents();
      expect(components.length).toBe(2);
      expect(components.map(c => c.componentId)).toEqual(jasmine.arrayContaining([component1, component2]));
    });

    it('should get polling documents for current route', () => {
      const componentId = 'test-component';
      const documentIds = ['doc-1', 'doc-2'];
      
      Object.defineProperty(mockRouter, 'url', { value: '/current-route', configurable: true });
      service.registerComponent(componentId, documentIds);
      
      const routeDocuments = service.getPollingDocumentsForRoute();
      expect(routeDocuments).toEqual(jasmine.arrayContaining(documentIds));
    });

    it('should return empty array for route with no polling documents', () => {
      const routeDocuments = service.getPollingDocumentsForRoute('/empty-route');
      expect(routeDocuments).toEqual([]);
    });
  });

  describe('browser event handling', () => {
    it('should handle page visibility changes', () => {
      spyOn(console, 'log');
      
      // Simulate page becoming hidden
      Object.defineProperty(document, 'hidden', { value: true, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
      
      expect(console.log).toHaveBeenCalledWith(
        'PollingLifecycleService: Pausing aggressive polling (page hidden)'
      );
      
      // Simulate page becoming visible
      Object.defineProperty(document, 'hidden', { value: false, configurable: true });
      document.dispatchEvent(new Event('visibilitychange'));
      
      expect(console.log).toHaveBeenCalledWith(
        'PollingLifecycleService: Resuming normal polling (page visible)'
      );
    });

    it('should handle page unload events', () => {
      spyOn(service, 'cleanupAllPolling');
      
      // Simulate beforeunload event
      window.dispatchEvent(new Event('beforeunload'));
      
      expect(service.cleanupAllPolling).toHaveBeenCalled();
    });
  });
});