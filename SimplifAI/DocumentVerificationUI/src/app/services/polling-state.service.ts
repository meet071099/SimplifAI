import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface PollingSession {
  documentId: string;
  documentType: string;
  sessionId: string;
  status: 'active' | 'completed' | 'failed' | 'timeout' | 'cancelled';
  startTime: number;
  endTime?: number;
  totalRequests: number;
  successfulRequests: number;
  failedRequests: number;
  currentRetryCount: number;
  maxRetries: number;
  lastRequestTime: number;
  lastResponseTime?: number;
  lastError?: {
    status?: number;
    message?: string;
    timestamp: number;
  };
  timerId?: any;
}

export interface PollingConfig {
  initialDelay: number;
  retryIntervals: number[];
  maxRetries: number;
  backoffMultiplier: number;
  timeoutMs: number;
  jitterMaxMs: number;
}

export interface PollingStateUpdate {
  sessionId: string;
  documentId: string;
  status: PollingSession['status'];
  timestamp: number;
  details?: {
    requestCount?: number;
    errorMessage?: string;
    responseTime?: number;
  };
}

@Injectable({
  providedIn: 'root'
})
export class PollingStateService {
  private pollingSessions = new Map<string, PollingSession>();
  private pollingStateSubject = new BehaviorSubject<Map<string, PollingSession>>(new Map());
  private pollingUpdatesSubject = new BehaviorSubject<PollingStateUpdate | null>(null);

  public pollingState$ = this.pollingStateSubject.asObservable();
  public pollingUpdates$ = this.pollingUpdatesSubject.asObservable();

  constructor() {
    // Restore polling state from session storage on service initialization
    this.restorePollingState();
  }

  /**
   * Start a new polling session
   * Prevents multiple concurrent polling for the same document
   */
  public startPollingSession(
    documentId: string,
    documentType: string,
    config: PollingConfig
  ): string {
    // Check if polling is already active for this document
    if (this.isPollingActive(documentId)) {
      console.log(`PollingStateService: Polling already active for document ${documentId}`);
      return this.getPollingSession(documentId)?.sessionId || '';
    }

    const sessionId = this.generateSessionId();
    const session: PollingSession = {
      documentId,
      documentType,
      sessionId,
      status: 'active',
      startTime: Date.now(),
      totalRequests: 0,
      successfulRequests: 0,
      failedRequests: 0,
      currentRetryCount: 0,
      maxRetries: config.maxRetries,
      lastRequestTime: Date.now()
    };

    this.pollingSessions.set(documentId, session);
    this.persistPollingState();
    this.notifyStateChange();

    // Emit polling update
    this.emitPollingUpdate({
      sessionId,
      documentId,
      status: 'active',
      timestamp: Date.now(),
      details: {
        requestCount: 0
      }
    });

    console.log(`PollingStateService: Started polling session for document ${documentId}:`, {
      sessionId,
      documentType,
      config
    });

    return sessionId;
  }

  /**
   * Stop polling session and clean up resources
   */
  public stopPollingSession(
    documentId: string,
    finalStatus: 'completed' | 'failed' | 'timeout' | 'cancelled',
    error?: { status?: number; message?: string }
  ): void {
    const session = this.pollingSessions.get(documentId);
    if (!session) {
      console.warn(`PollingStateService: Cannot stop polling - no session found for document ${documentId}`);
      return;
    }

    // Clear any active timer
    if (session.timerId) {
      clearTimeout(session.timerId);
      session.timerId = undefined;
    }

    // Update session with final status
    session.status = finalStatus;
    session.endTime = Date.now();
    if (error) {
      session.lastError = {
        ...error,
        timestamp: Date.now()
      };
    }

    this.persistPollingState();
    this.notifyStateChange();

    // Emit polling update
    this.emitPollingUpdate({
      sessionId: session.sessionId,
      documentId,
      status: finalStatus,
      timestamp: Date.now(),
      details: {
        requestCount: session.totalRequests,
        errorMessage: error?.message
      }
    });

    const duration = session.endTime - session.startTime;
    console.log(`PollingStateService: Stopped polling session for document ${documentId}:`, {
      sessionId: session.sessionId,
      finalStatus,
      duration: `${duration}ms`,
      totalRequests: session.totalRequests,
      successfulRequests: session.successfulRequests,
      failedRequests: session.failedRequests,
      error
    });

    // Remove session after a delay to allow for final logging
    setTimeout(() => {
      this.removePollingSession(documentId);
    }, 5000);
  }

  /**
   * Update polling session state
   */
  public updatePollingSession(
    documentId: string,
    updates: Partial<PollingSession>
  ): void {
    const session = this.pollingSessions.get(documentId);
    if (!session) {
      console.warn(`PollingStateService: Cannot update polling session - no session found for document ${documentId}`);
      return;
    }

    // Apply updates
    Object.assign(session, updates);
    this.persistPollingState();
    this.notifyStateChange();

    console.log(`PollingStateService: Updated polling session for document ${documentId}:`, {
      sessionId: session.sessionId,
      updates
    });
  }

  /**
   * Get polling session for a document
   */
  public getPollingSession(documentId: string): PollingSession | undefined {
    return this.pollingSessions.get(documentId);
  }

  /**
   * Check if polling is active for a document
   */
  public isPollingActive(documentId: string): boolean {
    const session = this.pollingSessions.get(documentId);
    return session?.status === 'active';
  }

  /**
   * Get all active polling sessions
   */
  public getActivePollingDocuments(): string[] {
    return Array.from(this.pollingSessions.entries())
      .filter(([_, session]) => session.status === 'active')
      .map(([documentId, _]) => documentId);
  }

  /**
   * Get all polling sessions
   */
  public getAllPollingSessions(): PollingSession[] {
    return Array.from(this.pollingSessions.values());
  }

  /**
   * Query polling status for a document
   */
  public queryPollingStatus(documentId: string): {
    isActive: boolean;
    session?: PollingSession;
    duration?: number;
    requestCount?: number;
  } {
    const session = this.pollingSessions.get(documentId);
    if (!session) {
      return { isActive: false };
    }

    const duration = (session.endTime || Date.now()) - session.startTime;
    return {
      isActive: session.status === 'active',
      session,
      duration,
      requestCount: session.totalRequests
    };
  }

  /**
   * Cancel polling for a specific document
   */
  public cancelPolling(documentId: string): void {
    this.stopPollingSession(documentId, 'cancelled');
  }

  /**
   * Cancel all active polling sessions
   */
  public cancelAllPolling(): void {
    const activeDocuments = this.getActivePollingDocuments();
    console.log(`PollingStateService: Cancelling ${activeDocuments.length} active polling sessions`);

    for (const documentId of activeDocuments) {
      this.cancelPolling(documentId);
    }
  }

  /**
   * Clean up resources for a specific document
   */
  public cleanupPollingResources(documentId: string): void {
    const session = this.pollingSessions.get(documentId);
    if (session) {
      // Clear any active timer
      if (session.timerId) {
        clearTimeout(session.timerId);
      }
      
      // If session is still active, mark as cancelled
      if (session.status === 'active') {
        this.stopPollingSession(documentId, 'cancelled');
      } else {
        // Just remove the session
        this.removePollingSession(documentId);
      }
    }
  }

  /**
   * Clean up all polling resources
   */
  public cleanupAllPollingResources(): void {
    console.log(`PollingStateService: Cleaning up ${this.pollingSessions.size} polling sessions`);

    // Clear all timers and cancel active sessions
    this.pollingSessions.forEach((session, documentId) => {
      if (session.timerId) {
        clearTimeout(session.timerId);
      }
      
      if (session.status === 'active') {
        session.status = 'cancelled';
        session.endTime = Date.now();
      }
    });

    this.pollingSessions.clear();
    this.persistPollingState();
    this.notifyStateChange();

    console.log('PollingStateService: All polling resources cleaned up');
  }

  /**
   * Set timer ID for a polling session
   */
  public setPollingTimer(documentId: string, timerId: any): void {
    const session = this.pollingSessions.get(documentId);
    if (session) {
      // Clear existing timer if any
      if (session.timerId) {
        clearTimeout(session.timerId);
      }
      
      session.timerId = timerId;
      console.log(`PollingStateService: Set timer for document ${documentId}:`, { timerId });
    }
  }

  /**
   * Clear timer for a polling session
   */
  public clearPollingTimer(documentId: string): void {
    const session = this.pollingSessions.get(documentId);
    if (session && session.timerId) {
      clearTimeout(session.timerId);
      session.timerId = undefined;
      console.log(`PollingStateService: Cleared timer for document ${documentId}`);
    }
  }

  /**
   * Persist polling state to session storage
   */
  private persistPollingState(): void {
    try {
      const stateData = Array.from(this.pollingSessions.entries()).map(([documentId, session]) => ({
        documentId,
        session: {
          ...session,
          timerId: undefined // Don't persist timer IDs
        }
      }));

      sessionStorage.setItem('pollingState', JSON.stringify(stateData));
    } catch (error) {
      console.warn('PollingStateService: Failed to persist polling state:', error);
    }
  }

  /**
   * Restore polling state from session storage
   */
  private restorePollingState(): void {
    try {
      const stateData = sessionStorage.getItem('pollingState');
      if (stateData) {
        const parsedData = JSON.parse(stateData);
        
        for (const { documentId, session } of parsedData) {
          // Only restore non-completed sessions
          if (session.status === 'active') {
            // Mark as cancelled since we can't restore active timers
            session.status = 'cancelled';
            session.endTime = Date.now();
          }
          
          this.pollingSessions.set(documentId, session);
        }

        this.notifyStateChange();
        console.log(`PollingStateService: Restored ${parsedData.length} polling sessions from storage`);
      }
    } catch (error) {
      console.warn('PollingStateService: Failed to restore polling state:', error);
    }
  }

  /**
   * Remove polling session from memory
   */
  private removePollingSession(documentId: string): void {
    const session = this.pollingSessions.get(documentId);
    if (session) {
      this.pollingSessions.delete(documentId);
      this.persistPollingState();
      this.notifyStateChange();
      
      console.log(`PollingStateService: Removed polling session for document ${documentId}:`, {
        sessionId: session.sessionId
      });
    }
  }

  /**
   * Notify subscribers of state changes
   */
  private notifyStateChange(): void {
    this.pollingStateSubject.next(new Map(this.pollingSessions));
  }

  /**
   * Emit polling update event
   */
  private emitPollingUpdate(update: PollingStateUpdate): void {
    this.pollingUpdatesSubject.next(update);
  }

  /**
   * Generate unique session ID
   */
  private generateSessionId(): string {
    return `polling_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Get polling statistics
   */
  public getPollingStatistics(): {
    totalSessions: number;
    activeSessions: number;
    completedSessions: number;
    failedSessions: number;
    timeoutSessions: number;
    cancelledSessions: number;
    averageSessionDuration: number;
    totalRequests: number;
    successRate: number;
  } {
    const sessions = Array.from(this.pollingSessions.values());
    const completedSessions = sessions.filter(s => s.endTime);
    
    const averageSessionDuration = completedSessions.length > 0
      ? completedSessions.reduce((sum, s) => sum + (s.endTime! - s.startTime), 0) / completedSessions.length
      : 0;

    const totalRequests = sessions.reduce((sum, s) => sum + s.totalRequests, 0);
    const totalSuccessfulRequests = sessions.reduce((sum, s) => sum + s.successfulRequests, 0);
    const successRate = totalRequests > 0 ? (totalSuccessfulRequests / totalRequests) * 100 : 0;

    return {
      totalSessions: sessions.length,
      activeSessions: sessions.filter(s => s.status === 'active').length,
      completedSessions: sessions.filter(s => s.status === 'completed').length,
      failedSessions: sessions.filter(s => s.status === 'failed').length,
      timeoutSessions: sessions.filter(s => s.status === 'timeout').length,
      cancelledSessions: sessions.filter(s => s.status === 'cancelled').length,
      averageSessionDuration: Math.round(averageSessionDuration),
      totalRequests,
      successRate: Math.round(successRate * 100) / 100
    };
  }
}