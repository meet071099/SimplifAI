import { OnDestroy, OnInit, inject } from '@angular/core';
import { PollingLifecycleService } from '../services/polling-lifecycle.service';
import { PollingStateService } from '../services/polling-state.service';

/**
 * Interface for components that use polling lifecycle management
 */
export interface PollingLifecycleComponent {
  componentId: string;
  pollingDocumentIds: string[];
  onPollingStateChange?(documentId: string, status: string): void;
}

/**
 * Mixin function to add polling lifecycle management to components
 */
export function withPollingLifecycle<T extends new (...args: any[]) => {}>(Base: T) {
  return class extends Base implements OnInit, OnDestroy, PollingLifecycleComponent {
    public componentId: string = '';
    public pollingDocumentIds: string[] = [];
    
    private pollingLifecycleService = inject(PollingLifecycleService);
    private pollingStateService = inject(PollingStateService);
    private isPollingLifecycleInitialized = false;

    ngOnInit(): void {
      // Call parent ngOnInit if it exists
      if (super.ngOnInit) {
        super.ngOnInit();
      }
      
      this.initializePollingLifecycle();
    }

    ngOnDestroy(): void {
      this.cleanupPollingLifecycle();
      
      // Call parent ngOnDestroy if it exists
      if (super.ngOnDestroy) {
        super.ngOnDestroy();
      }
    }

    /**
     * Initialize polling lifecycle management
     */
    protected initializePollingLifecycle(): void {
      if (this.isPollingLifecycleInitialized) {
        return;
      }

      // Generate component ID if not set
      if (!this.componentId) {
        this.componentId = this.generateComponentId();
      }

      // Register component with lifecycle service
      this.pollingLifecycleService.registerComponent(
        this.componentId,
        this.pollingDocumentIds
      );

      // Subscribe to polling state changes
      this.pollingStateService.pollingUpdates$.subscribe(update => {
        if (update && this.pollingDocumentIds.includes(update.documentId)) {
          this.handlePollingStateChange(update.documentId, update.status);
        }
      });

      this.isPollingLifecycleInitialized = true;
      
      console.log(`PollingLifecycleMixin: Initialized polling lifecycle for component ${this.componentId}:`, {
        documentIds: this.pollingDocumentIds
      });
    }

    /**
     * Cleanup polling lifecycle management
     */
    protected cleanupPollingLifecycle(): void {
      if (this.isPollingLifecycleInitialized) {
        this.pollingLifecycleService.unregisterComponent(this.componentId);
        this.isPollingLifecycleInitialized = false;
        
        console.log(`PollingLifecycleMixin: Cleaned up polling lifecycle for component ${this.componentId}`);
      }
    }

    /**
     * Add document to polling lifecycle management
     */
    protected addPollingDocument(documentId: string): void {
      if (!this.pollingDocumentIds.includes(documentId)) {
        this.pollingDocumentIds.push(documentId);
        
        if (this.isPollingLifecycleInitialized) {
          this.pollingLifecycleService.addDocumentToComponent(this.componentId, documentId);
        }
        
        console.log(`PollingLifecycleMixin: Added document ${documentId} to component ${this.componentId}`);
      }
    }

    /**
     * Remove document from polling lifecycle management
     */
    protected removePollingDocument(documentId: string): void {
      const index = this.pollingDocumentIds.indexOf(documentId);
      if (index > -1) {
        this.pollingDocumentIds.splice(index, 1);
        
        if (this.isPollingLifecycleInitialized) {
          this.pollingLifecycleService.removeDocumentFromComponent(this.componentId, documentId);
        }
        
        console.log(`PollingLifecycleMixin: Removed document ${documentId} from component ${this.componentId}`);
      }
    }

    /**
     * Handle polling state changes
     */
    protected handlePollingStateChange(documentId: string, status: string): void {
      // Call the optional callback if implemented
      if (this.onPollingStateChange) {
        this.onPollingStateChange(documentId, status);
      }
      
      console.log(`PollingLifecycleMixin: Polling state changed for document ${documentId}:`, {
        componentId: this.componentId,
        status
      });
    }

    /**
     * Check if document is being polled
     */
    protected isDocumentPolling(documentId: string): boolean {
      return this.pollingStateService.isPollingActive(documentId);
    }

    /**
     * Get polling status for document
     */
    protected getDocumentPollingStatus(documentId: string) {
      return this.pollingStateService.queryPollingStatus(documentId);
    }

    /**
     * Cancel polling for a specific document
     */
    protected cancelDocumentPolling(documentId: string): void {
      this.pollingStateService.cancelPolling(documentId);
      this.removePollingDocument(documentId);
    }

    /**
     * Cancel all polling for this component
     */
    protected cancelAllPolling(): void {
      const documentIds = [...this.pollingDocumentIds];
      documentIds.forEach(documentId => {
        this.pollingStateService.cancelPolling(documentId);
      });
      this.pollingDocumentIds = [];
      
      console.log(`PollingLifecycleMixin: Cancelled all polling for component ${this.componentId}`);
    }

    /**
     * Generate unique component ID
     */
    private generateComponentId(): string {
      const timestamp = Date.now();
      const random = Math.random().toString(36).substr(2, 9);
      const className = this.constructor.name;
      return `${className}_${timestamp}_${random}`;
    }

    /**
     * Optional callback for polling state changes
     * Components can override this method to handle state changes
     */
    onPollingStateChange?(documentId: string, status: string): void;
  };
}

/**
 * Base class for components that need polling lifecycle management
 */
export abstract class PollingLifecycleBaseComponent implements OnInit, OnDestroy, PollingLifecycleComponent {
  public componentId: string = '';
  public pollingDocumentIds: string[] = [];
  
  private pollingLifecycleService = inject(PollingLifecycleService);
  private pollingStateService = inject(PollingStateService);
  private isPollingLifecycleInitialized = false;

  ngOnInit(): void {
    this.initializePollingLifecycle();
  }

  ngOnDestroy(): void {
    this.cleanupPollingLifecycle();
  }

  /**
   * Initialize polling lifecycle management
   */
  protected initializePollingLifecycle(): void {
    if (this.isPollingLifecycleInitialized) {
      return;
    }

    // Generate component ID if not set
    if (!this.componentId) {
      this.componentId = this.generateComponentId();
    }

    // Register component with lifecycle service
    this.pollingLifecycleService.registerComponent(
      this.componentId,
      this.pollingDocumentIds
    );

    // Subscribe to polling state changes
    this.pollingStateService.pollingUpdates$.subscribe(update => {
      if (update && this.pollingDocumentIds.includes(update.documentId)) {
        this.handlePollingStateChange(update.documentId, update.status);
      }
    });

    this.isPollingLifecycleInitialized = true;
    
    console.log(`PollingLifecycleBaseComponent: Initialized polling lifecycle for component ${this.componentId}:`, {
      documentIds: this.pollingDocumentIds
    });
  }

  /**
   * Cleanup polling lifecycle management
   */
  protected cleanupPollingLifecycle(): void {
    if (this.isPollingLifecycleInitialized) {
      this.pollingLifecycleService.unregisterComponent(this.componentId);
      this.isPollingLifecycleInitialized = false;
      
      console.log(`PollingLifecycleBaseComponent: Cleaned up polling lifecycle for component ${this.componentId}`);
    }
  }

  /**
   * Add document to polling lifecycle management
   */
  protected addPollingDocument(documentId: string): void {
    if (!this.pollingDocumentIds.includes(documentId)) {
      this.pollingDocumentIds.push(documentId);
      
      if (this.isPollingLifecycleInitialized) {
        this.pollingLifecycleService.addDocumentToComponent(this.componentId, documentId);
      }
      
      console.log(`PollingLifecycleBaseComponent: Added document ${documentId} to component ${this.componentId}`);
    }
  }

  /**
   * Remove document from polling lifecycle management
   */
  protected removePollingDocument(documentId: string): void {
    const index = this.pollingDocumentIds.indexOf(documentId);
    if (index > -1) {
      this.pollingDocumentIds.splice(index, 1);
      
      if (this.isPollingLifecycleInitialized) {
        this.pollingLifecycleService.removeDocumentFromComponent(this.componentId, documentId);
      }
      
      console.log(`PollingLifecycleBaseComponent: Removed document ${documentId} from component ${this.componentId}`);
    }
  }

  /**
   * Handle polling state changes
   */
  protected handlePollingStateChange(documentId: string, status: string): void {
    // Call the optional callback if implemented
    if (this.onPollingStateChange) {
      this.onPollingStateChange(documentId, status);
    }
    
    console.log(`PollingLifecycleBaseComponent: Polling state changed for document ${documentId}:`, {
      componentId: this.componentId,
      status
    });
  }

  /**
   * Check if document is being polled
   */
  protected isDocumentPolling(documentId: string): boolean {
    return this.pollingStateService.isPollingActive(documentId);
  }

  /**
   * Get polling status for document
   */
  protected getDocumentPollingStatus(documentId: string) {
    return this.pollingStateService.queryPollingStatus(documentId);
  }

  /**
   * Cancel polling for a specific document
   */
  protected cancelDocumentPolling(documentId: string): void {
    this.pollingStateService.cancelPolling(documentId);
    this.removePollingDocument(documentId);
  }

  /**
   * Cancel all polling for this component
   */
  protected cancelAllPolling(): void {
    const documentIds = [...this.pollingDocumentIds];
    documentIds.forEach(documentId => {
      this.pollingStateService.cancelPolling(documentId);
    });
    this.pollingDocumentIds = [];
    
    console.log(`PollingLifecycleBaseComponent: Cancelled all polling for component ${this.componentId}`);
  }

  /**
   * Generate unique component ID
   */
  private generateComponentId(): string {
    const timestamp = Date.now();
    const random = Math.random().toString(36).substr(2, 9);
    const className = this.constructor.name;
    return `${className}_${timestamp}_${random}`;
  }

  /**
   * Optional callback for polling state changes
   * Components can override this method to handle state changes
   */
  onPollingStateChange?(documentId: string, status: string): void;
}