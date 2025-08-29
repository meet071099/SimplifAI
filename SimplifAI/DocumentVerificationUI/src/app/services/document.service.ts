import { Injectable } from '@angular/core';
import { HttpClient, HttpEventType, HttpRequest } from '@angular/common/http';
import { BehaviorSubject, Observable, Subject, throwError, of, timer } from 'rxjs';
import { map, catchError, tap, delay, switchMap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { PerformanceMonitoringService } from './performance-monitoring.service';
import { PollingStateService } from './polling-state.service';

// Extend environment interface to include demoMode
declare module '../../environments/environment' {
  interface Environment {
    demoMode?: boolean;
  }
}

export interface DocumentUploadProgress {
  documentType: string;
  progress: number;
  status: 'uploading' | 'processing' | 'completed' | 'error';
  message?: string;
}

export interface DocumentVerificationResult {
  documentId: string;
  verificationStatus: 'verified' | 'failed' | 'pending';
  confidenceScore?: number;
  isBlurred: boolean;
  isCorrectType: boolean;
  statusColor: 'green' | 'yellow' | 'red';
  message: string;
  requiresUserConfirmation: boolean;
}

export interface DocumentUploadRequest {
  file: File;
  documentType: string;
  formId: string;
}

export interface ErrorResponse {
  type: 'security' | 'network' | 'azure_intelligence' | 'server';
  message: string;
  actionable: boolean;
  suggestedAction?: string;
  originalError?: any;
}

export interface AzureIntelligenceError {
  code: string;
  message: string;
  details?: {
    fileSize?: number;
    maxAllowedSize?: number;
    documentType?: string;
    confidenceScore?: number;
    rejectionReason?: string;
  };
}

export interface StatusPollingConfig {
  initialDelay: number;
  retryIntervals: number[];
  maxRetries: number;
  backoffMultiplier: number;
  timeoutMs: number;
  jitterMaxMs: number;
}

export interface StatusRequestContext {
  documentId: string;
  documentType: string;
  uploadTimestamp: number;
  retryCount: number;
  lastAttemptTimestamp: number;
  maxRetries: number;
  pollingStartTime: number;
}

export interface StatusErrorResponse extends ErrorResponse {
  documentId: string;
  documentExists: boolean;
  suggestedWaitTime?: number;
  retryRecommended: boolean;
  recoveryAction?: 'automatic_retry' | 'manual_retry' | 'no_retry';
  recoveryDelay?: number;
}

export interface ApiRequestLog {
  timestamp: string;
  method: string;
  url: string;
  requestId: string;
  requestBody?: any;
  headers?: any;
  fileInfo?: {
    name: string;
    size: number;
    type: string;
  };
}

export interface ApiResponseLog {
  timestamp: string;
  requestId: string;
  status: number;
  statusText: string;
  responseBody?: any;
  duration: number;
  success: boolean;
}

export interface ApiErrorLog {
  timestamp: string;
  requestId: string;
  method: string;
  url: string;
  status?: number;
  statusText?: string;
  errorMessage: string;
  errorDetails?: any;
  duration: number;
  context: {
    operation: string;
    documentType?: string;
    fileSize?: number;
    retryAttempt?: number;
  };
}

export interface StatusRequestLog {
  timestamp: string;
  requestId: string;
  documentId: string;
  documentType: string;
  operation: 'status_request' | 'status_polling_start' | 'status_polling_retry' | 'status_polling_success' | 'status_polling_failure' | 'status_polling_timeout';
  url: string;
  retryCount?: number;
  backoffInterval?: number;
  totalElapsedTime?: number;
  pollingSessionId?: string;
  result?: 'success' | 'failure' | 'timeout' | 'cancelled';
  errorDetails?: {
    status?: number;
    statusText?: string;
    errorMessage?: string;
    errorType?: string;
  };
  pollingContext?: {
    maxRetries: number;
    timeoutMs: number;
    pollingStartTime: number;
    lastAttemptTimestamp: number;
  };
}

export interface PollingSessionState {
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
  config: StatusPollingConfig;
  timerId?: any;
}

export interface PollingStateEvent {
  timestamp: string;
  sessionId: string;
  documentId: string;
  eventType: 'session_start' | 'request_sent' | 'request_success' | 'request_failure' | 'retry_scheduled' | 'session_timeout' | 'session_cancelled' | 'session_completed';
  details?: {
    requestId?: string;
    retryCount?: number;
    backoffInterval?: number;
    errorStatus?: number;
    errorMessage?: string;
    responseTime?: number;
    totalElapsedTime?: number;
  };
}

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private baseUrl = environment.apiUrl;
  private uploadProgressSubject = new BehaviorSubject<DocumentUploadProgress[]>([]);
  private verificationUpdatesSubject = new Subject<DocumentVerificationResult>();
  private uploadedDocumentIds = new Set<string>(); // Track successfully uploaded document IDs

  public uploadProgress$ = this.uploadProgressSubject.asObservable();
  public verificationUpdates$ = this.verificationUpdatesSubject.asObservable();

  // Enhanced status polling configuration
  private readonly defaultPollingConfig: StatusPollingConfig = {
    initialDelay: 1000,      // 1 second initial delay
    retryIntervals: [2000, 5000, 10000, 30000], // 2s, 5s, 10s, 30s
    maxRetries: 10,
    backoffMultiplier: 1.5,
    timeoutMs: 300000,       // 5 minutes total timeout
    jitterMaxMs: 1000        // Up to 1 second jitter
  };

  // Active polling sessions tracking
  private activePollingContexts = new Map<string, StatusRequestContext>();
  private pollingTimers = new Map<string, any>();
  
  // Enhanced polling state tracking
  private pollingSessionStates = new Map<string, PollingSessionState>();
  private pollingStateEvents: PollingStateEvent[] = [];

  constructor(
    private http: HttpClient,
    private performanceMonitoring: PerformanceMonitoringService,
    private pollingStateService: PollingStateService
  ) {}

  /**
   * Get polling configuration with optional overrides
   */
  public getPollingConfig(overrides?: Partial<StatusPollingConfig>): StatusPollingConfig {
    return {
      ...this.defaultPollingConfig,
      ...overrides
    };
  }

  /**
   * Set custom polling configuration
   */
  public setPollingConfig(config: Partial<StatusPollingConfig>): void {
    Object.assign(this.defaultPollingConfig, config);
    console.log('DocumentService: Updated polling configuration:', this.defaultPollingConfig);
  }

  /**
   * Calculate next retry interval with exponential backoff and jitter
   */
  private calculateRetryInterval(retryCount: number, config: StatusPollingConfig): number {
    // Use predefined intervals if available
    if (retryCount < config.retryIntervals.length) {
      const baseInterval = config.retryIntervals[retryCount];
      return this.addJitter(baseInterval, config.jitterMaxMs);
    }

    // Calculate exponential backoff for retries beyond predefined intervals
    const lastInterval = config.retryIntervals[config.retryIntervals.length - 1];
    const exponentialInterval = lastInterval * Math.pow(config.backoffMultiplier, retryCount - config.retryIntervals.length + 1);
    
    // Cap at maximum reasonable interval (2 minutes)
    const cappedInterval = Math.min(exponentialInterval, 120000);
    
    return this.addJitter(cappedInterval, config.jitterMaxMs);
  }

  /**
   * Add random jitter to prevent thundering herd
   */
  private addJitter(baseInterval: number, maxJitterMs: number): number {
    const jitter = Math.random() * maxJitterMs;
    return Math.round(baseInterval + jitter);
  }

  /**
   * Check if polling has exceeded timeout
   */
  private hasPollingTimedOut(context: StatusRequestContext, config: StatusPollingConfig): boolean {
    const elapsedTime = Date.now() - context.pollingStartTime;
    return elapsedTime >= config.timeoutMs;
  }

  /**
   * Check if maximum retries exceeded
   */
  private hasExceededMaxRetries(context: StatusRequestContext, config: StatusPollingConfig): boolean {
    return context.retryCount >= config.maxRetries;
  }

  /**
   * Create status request context for polling
   */
  private createStatusRequestContext(documentId: string, documentType: string): StatusRequestContext {
    return {
      documentId,
      documentType,
      uploadTimestamp: Date.now(),
      retryCount: 0,
      lastAttemptTimestamp: 0,
      maxRetries: this.defaultPollingConfig.maxRetries,
      pollingStartTime: Date.now()
    };
  }

  /**
   * Start polling session for document status
   */
  private startPollingSession(documentId: string, documentType: string): void {
    // Prevent multiple concurrent polling for same document using the new polling state service
    if (this.pollingStateService.isPollingActive(documentId)) {
      console.log(`DocumentService: Polling already active for document ${documentId}`);
      return;
    }

    const context = this.createStatusRequestContext(documentId, documentType);
    this.activePollingContexts.set(documentId, context);
    
    // Start polling session in the new polling state service
    const sessionId = this.pollingStateService.startPollingSession(
      documentId, 
      documentType, 
      this.defaultPollingConfig
    );
    
    // Create polling session state for enhanced tracking (legacy)
    this.createPollingSessionState(documentId, documentType, sessionId, this.defaultPollingConfig);
    
    console.log(`DocumentService: Started polling session for document ${documentId}:`, {
      documentType,
      sessionId,
      config: this.defaultPollingConfig,
      context
    });
  }

  /**
   * Stop polling session and cleanup resources
   */
  private stopPollingSession(documentId: string, reason: string): void {
    // Clear any active timer
    const timer = this.pollingTimers.get(documentId);
    if (timer) {
      clearTimeout(timer);
      this.pollingTimers.delete(documentId);
    }

    // Remove polling context
    const context = this.activePollingContexts.get(documentId);
    this.activePollingContexts.delete(documentId);

    // Complete polling session state
    const sessionState = this.getPollingSessionState(documentId);
    if (sessionState) {
      let finalStatus: 'completed' | 'failed' | 'timeout' | 'cancelled';
      let finalError: { status?: number; message?: string } | undefined;

      switch (reason) {
        case 'success':
          finalStatus = 'completed';
          break;
        case 'timeout':
          finalStatus = 'timeout';
          finalError = { message: 'Polling timeout reached' };
          break;
        case 'max_retries_exceeded':
          finalStatus = 'failed';
          finalError = { message: 'Maximum retry attempts exceeded' };
          break;
        case 'error_no_retry':
          finalStatus = 'failed';
          finalError = { message: 'Unrecoverable error occurred' };
          break;
        case 'manual_cleanup':
          finalStatus = 'cancelled';
          finalError = { message: 'Manually cancelled' };
          break;
        default:
          finalStatus = 'failed';
          finalError = { message: reason };
      }

      // Stop polling session in the new polling state service
      this.pollingStateService.stopPollingSession(documentId, finalStatus, finalError);
      
      this.completePollingSessionState(documentId, finalStatus, finalError);
    }

    if (context) {
      const duration = Date.now() - context.pollingStartTime;
      console.log(`DocumentService: Stopped polling session for document ${documentId}:`, {
        reason,
        duration: `${duration}ms`,
        totalRetries: context.retryCount,
        sessionId: sessionState?.sessionId,
        context
      });
    }

    // Clear session state after a delay to allow for final logging
    setTimeout(() => {
      this.clearPollingSessionState(documentId);
    }, 1000);
  }

  /**
   * Get active polling context for document
   */
  private getPollingContext(documentId: string): StatusRequestContext | undefined {
    return this.activePollingContexts.get(documentId);
  }

  /**
   * Check if document is currently being polled
   */
  public isPollingActive(documentId: string): boolean {
    return this.activePollingContexts.has(documentId);
  }

  /**
   * Get all active polling sessions
   */
  public getActivePollingDocuments(): string[] {
    return Array.from(this.activePollingContexts.keys());
  }

  /**
   * Schedule next retry attempt with exponential backoff
   */
  private scheduleRetry(
    documentId: string, 
    retryFunction: () => void, 
    config: StatusPollingConfig
  ): void {
    const context = this.getPollingContext(documentId);
    if (!context) {
      console.warn(`DocumentService: Cannot schedule retry - no polling context for document ${documentId}`);
      return;
    }

    const totalElapsedTime = Date.now() - context.pollingStartTime;

    // Check timeout and max retries before scheduling
    if (this.hasPollingTimedOut(context, config)) {
      console.log(`DocumentService: Polling timeout reached for document ${documentId}`);
      
      // Log polling timeout
      this.logStatusRequest('status_polling_timeout', documentId, context.documentType, 
        this.generateRequestId(), `${this.baseUrl}/api/Document/${documentId}/status`, {
        retryCount: context.retryCount,
        totalElapsedTime,
        result: 'timeout',
        pollingContext: {
          maxRetries: config.maxRetries,
          timeoutMs: config.timeoutMs,
          pollingStartTime: context.pollingStartTime,
          lastAttemptTimestamp: context.lastAttemptTimestamp
        }
      });
      
      this.stopPollingSession(documentId, 'timeout');
      return;
    }

    if (this.hasExceededMaxRetries(context, config)) {
      console.log(`DocumentService: Max retries exceeded for document ${documentId}`);
      
      // Log max retries exceeded
      this.logStatusRequest('status_polling_failure', documentId, context.documentType, 
        this.generateRequestId(), `${this.baseUrl}/api/Document/${documentId}/status`, {
        retryCount: context.retryCount,
        totalElapsedTime,
        result: 'failure',
        errorDetails: {
          errorMessage: 'Maximum retry attempts exceeded',
          errorType: 'MaxRetriesExceeded'
        },
        pollingContext: {
          maxRetries: config.maxRetries,
          timeoutMs: config.timeoutMs,
          pollingStartTime: context.pollingStartTime,
          lastAttemptTimestamp: context.lastAttemptTimestamp
        }
      });
      
      this.stopPollingSession(documentId, 'max_retries_exceeded');
      return;
    }

    // Calculate retry interval
    const retryInterval = this.calculateRetryInterval(context.retryCount, config);
    
    // Update polling session state for retry scheduling
    const sessionState = this.getPollingSessionState(documentId);
    if (sessionState) {
      this.updatePollingSessionState(documentId, {
        currentRetryCount: context.retryCount + 1
      });
      
      // Log polling state event for retry scheduling
      this.logPollingStateEvent(sessionState.sessionId, documentId, 'retry_scheduled', {
        retryCount: context.retryCount + 1,
        backoffInterval: retryInterval,
        totalElapsedTime
      });
    }

    // Log retry scheduling
    this.logStatusRequest('status_polling_retry', documentId, context.documentType, 
      this.generateRequestId(), `${this.baseUrl}/api/Document/${documentId}/status`, {
      retryCount: context.retryCount + 1,
      backoffInterval: retryInterval,
      totalElapsedTime,
      pollingContext: {
        maxRetries: config.maxRetries,
        timeoutMs: config.timeoutMs,
        pollingStartTime: context.pollingStartTime,
        lastAttemptTimestamp: context.lastAttemptTimestamp
      }
    });
    
    console.log(`DocumentService: Scheduling retry ${context.retryCount + 1} for document ${documentId}:`, {
      retryInterval: `${retryInterval}ms`,
      nextAttemptTime: new Date(Date.now() + retryInterval).toISOString(),
      totalElapsed: `${totalElapsedTime}ms`
    });

    // Clear any existing timer
    const existingTimer = this.pollingTimers.get(documentId);
    if (existingTimer) {
      clearTimeout(existingTimer);
    }

    // Schedule the retry
    const timer = setTimeout(() => {
      this.pollingTimers.delete(documentId);
      
      // Update context before retry
      context.retryCount++;
      context.lastAttemptTimestamp = Date.now();
      
      console.log(`DocumentService: Executing retry ${context.retryCount} for document ${documentId}`);
      retryFunction();
    }, retryInterval);

    this.pollingTimers.set(documentId, timer);
  }

  /**
   * Cleanup all polling sessions and timers
   */
  public cleanupAllPolling(): void {
    console.log(`DocumentService: Cleaning up ${this.activePollingContexts.size} active polling sessions`);
    
    // Clear all timers
    for (const [documentId, timer] of this.pollingTimers.entries()) {
      clearTimeout(timer);
      console.log(`DocumentService: Cleared timer for document ${documentId}`);
    }
    this.pollingTimers.clear();

    // Clear all contexts
    const documentIds = Array.from(this.activePollingContexts.keys());
    this.activePollingContexts.clear();
    
    // Cleanup all polling in the new polling state service
    this.pollingStateService.cleanupAllPollingResources();
    
    // Clear all polling session states (legacy)
    this.clearAllPollingSessionStates();
    
    console.log(`DocumentService: Cleaned up polling for documents:`, documentIds);
  }

  /**
   * Cleanup polling for specific document
   */
  public cleanupDocumentPolling(documentId: string): void {
    // Cleanup in the new polling state service
    this.pollingStateService.cleanupPollingResources(documentId);
    
    // Legacy cleanup
    this.stopPollingSession(documentId, 'manual_cleanup');
  }

  /**
   * Execute status request with graduated retry logic
   */
  private executeStatusRequestWithRetry(
    documentId: string,
    documentType: string,
    config?: Partial<StatusPollingConfig>
  ): Observable<DocumentVerificationResult> {
    const pollingConfig = this.getPollingConfig(config);
    const pollingSessionId = this.generateRequestId();
    
    // Start polling session if not already active
    if (!this.isPollingActive(documentId)) {
      this.startPollingSession(documentId, documentType);
    }

    const context = this.getPollingContext(documentId);
    if (!context) {
      return throwError(() => new Error(`Failed to create polling context for document ${documentId}`));
    }

    // Log polling session start
    const url = `${this.baseUrl}/api/Document/${documentId}/status`;
    this.logStatusRequest('status_polling_start', documentId, documentType, pollingSessionId, url, {
      pollingContext: {
        maxRetries: pollingConfig.maxRetries,
        timeoutMs: pollingConfig.timeoutMs,
        pollingStartTime: context.pollingStartTime,
        lastAttemptTimestamp: context.lastAttemptTimestamp
      }
    });

    // Create the retry function
    const attemptStatusRequest = (): Observable<DocumentVerificationResult> => {
      const requestId = this.generateRequestId();
      const startTime = Date.now();
      const totalElapsedTime = Date.now() - context.pollingStartTime;

      // Log individual status request attempt
      this.logStatusRequest('status_request', documentId, documentType, requestId, url, {
        retryCount: context.retryCount,
        totalElapsedTime,
        pollingSessionId
      });

      console.log(`DocumentService: Attempting status request for document ${documentId}:`, {
        attempt: context.retryCount + 1,
        maxRetries: pollingConfig.maxRetries,
        url,
        requestId,
        pollingSessionId,
        elapsedTime: `${totalElapsedTime}ms`
      });

      // Log the API request
      this.logApiRequest('GET', url, requestId, { documentId });

      return this.http.get<DocumentVerificationResult>(url).pipe(
        tap(result => {
          const finalElapsedTime = Date.now() - context.pollingStartTime;
          const responseTime = Date.now() - startTime;
          
          // Update polling session state for successful request
          this.updatePollingSessionState(documentId, {
            totalRequests: context.retryCount + 1,
            successfulRequests: (this.getPollingSessionState(documentId)?.successfulRequests || 0) + 1,
            lastResponseTime: Date.now()
          });
          
          // Log polling state event
          this.logPollingStateEvent(pollingSessionId, documentId, 'request_success', {
            requestId,
            retryCount: context.retryCount,
            responseTime,
            totalElapsedTime: finalElapsedTime
          });
          
          // Success - log response and status polling success
          this.logApiResponse(requestId, 200, 'OK', result, startTime);
          this.logStatusRequest('status_polling_success', documentId, documentType, requestId, url, {
            retryCount: context.retryCount,
            totalElapsedTime: finalElapsedTime,
            pollingSessionId,
            result: 'success'
          });
          
          this.stopPollingSession(documentId, 'success');
          
          console.log(`DocumentService: Status request successful for document ${documentId}:`, {
            result,
            totalRetries: context.retryCount,
            totalDuration: `${finalElapsedTime}ms`,
            responseTime: `${responseTime}ms`,
            pollingSessionId
          });
        }),
        catchError(error => {
          const finalElapsedTime = Date.now() - context.pollingStartTime;
          const responseTime = Date.now() - startTime;
          
          // Update polling session state for failed request
          this.updatePollingSessionState(documentId, {
            totalRequests: context.retryCount + 1,
            failedRequests: (this.getPollingSessionState(documentId)?.failedRequests || 0) + 1,
            lastError: {
              status: error.status,
              message: error.message,
              timestamp: Date.now()
            }
          });
          
          // Log polling state event
          this.logPollingStateEvent(pollingSessionId, documentId, 'request_failure', {
            requestId,
            retryCount: context.retryCount,
            responseTime,
            totalElapsedTime: finalElapsedTime,
            errorStatus: error.status,
            errorMessage: error.message
          });
          
          // Log the API error
          this.logApiError(requestId, 'GET', url, error, {
            operation: 'status_request',
            documentType: context.documentType,
            retryAttempt: context.retryCount + 1
          }, startTime);

          // Log status polling failure details
          this.logStatusRequest('status_polling_failure', documentId, documentType, requestId, url, {
            retryCount: context.retryCount,
            totalElapsedTime: finalElapsedTime,
            pollingSessionId,
            result: 'failure',
            errorDetails: {
              status: error.status,
              statusText: error.statusText,
              errorMessage: error.message,
              errorType: error.name
            }
          });

          console.log(`DocumentService: Status request failed for document ${documentId}:`, {
            error: error.status,
            message: error.message,
            attempt: context.retryCount + 1,
            responseTime: `${responseTime}ms`,
            pollingSessionId,
            willRetry: !this.hasPollingTimedOut(context, pollingConfig) && !this.hasExceededMaxRetries(context, pollingConfig)
          });

          // Check if we should retry using enhanced logic
          if (this.shouldRetryStatusRequest(error, context, pollingConfig)) {
            // Apply status-specific error recovery
            return this.applyStatusErrorRecovery(error, documentId, context).pipe(
              catchError(recoveryError => {
                // Schedule retry and return empty observable (retry will be handled by timer)
                return new Observable<DocumentVerificationResult>(subscriber => {
                  this.scheduleRetry(documentId, () => {
                    // Execute retry
                    attemptStatusRequest().subscribe({
                      next: result => subscriber.next(result),
                      error: err => subscriber.error(err),
                      complete: () => subscriber.complete()
                    });
                  }, pollingConfig);
                });
              })
            );
          } else {
            // Stop polling and apply final error recovery
            this.stopPollingSession(documentId, 'error_no_retry');
            return this.applyStatusErrorRecovery(error, documentId, context);
          }
        })
      );
    };

    // Start with initial delay if configured
    if (pollingConfig.initialDelay > 0) {
      return timer(pollingConfig.initialDelay).pipe(
        switchMap(() => attemptStatusRequest())
      );
    } else {
      return attemptStatusRequest();
    }
  }

  /**
   * Determine if status request should be retried with enhanced logic
   */
  private shouldRetryStatusRequest(
    error: any, 
    context: StatusRequestContext, 
    config: StatusPollingConfig
  ): boolean {
    // Don't retry if timeout or max retries exceeded
    if (this.hasPollingTimedOut(context, config) || this.hasExceededMaxRetries(context, config)) {
      return false;
    }

    // Use enhanced status-specific retry logic
    return this.shouldRetryStatusError(error, context, config);
  }

  /**
   * Enhanced status-specific retry logic
   * Handles different types of errors with appropriate recovery strategies
   */
  private shouldRetryStatusError(
    error: any, 
    context: StatusRequestContext, 
    config: StatusPollingConfig
  ): boolean {
    // Handle 404 errors with specific logic
    if (error.status === 404) {
      return this.shouldRetry404StatusError(error, context, config);
    }

    // Always retry server errors (5xx)
    if (error.status >= 500 && error.status < 600) {
      return true;
    }

    // Retry rate limiting errors with longer delays
    if (error.status === 429) {
      return true;
    }

    // Retry network timeouts and connectivity issues
    if (!error.status || error.name === 'TimeoutError' || error.message?.includes('timeout')) {
      return true;
    }

    // Don't retry other 4xx client errors (except 404 and 429)
    if (error.status >= 400 && error.status < 500) {
      return false;
    }

    // Retry unknown errors (may be network issues)
    return true;
  }

  /**
   * Determine if 404 status errors should be retried
   * Distinguishes between permanent and temporary 404 errors
   */
  private shouldRetry404StatusError(
    error: any, 
    context: StatusRequestContext, 
    config: StatusPollingConfig
  ): boolean {
    const isDocumentTracked = this.isDocumentTracked(context.documentId);
    const timeSinceUpload = Date.now() - context.uploadTimestamp;

    // Don't retry if document is not tracked (permanent 404)
    if (!isDocumentTracked) {
      console.log(`DocumentService: Not retrying 404 for untracked document ${context.documentId}`);
      return false;
    }

    // Always retry if document was recently uploaded (likely still processing)
    if (timeSinceUpload < 300000) { // 5 minutes
      console.log(`DocumentService: Retrying 404 for recently uploaded document ${context.documentId} (${timeSinceUpload}ms ago)`);
      return true;
    }

    // For older documents, be more conservative with retries
    if (context.retryCount < 3) {
      console.log(`DocumentService: Retrying 404 for older document ${context.documentId} (attempt ${context.retryCount + 1})`);
      return true;
    }

    console.log(`DocumentService: Not retrying 404 for old document ${context.documentId} after ${context.retryCount} attempts`);
    return false;
  }

  /**
   * Apply status-specific error recovery strategies
   */
  private applyStatusErrorRecovery(
    error: any, 
    documentId: string, 
    context: StatusRequestContext
  ): Observable<DocumentVerificationResult> {
    console.log(`DocumentService: Applying error recovery for document ${documentId}:`, {
      errorStatus: error.status,
      errorType: error.name,
      retryCount: context.retryCount,
      timeSinceUpload: Date.now() - context.uploadTimestamp
    });

    // Handle 404 errors with specific recovery
    if (error.status === 404) {
      return this.recover404StatusError(error, documentId, context);
    }

    // Handle server errors with exponential backoff
    if (error.status >= 500) {
      return this.recoverServerStatusError(error, documentId, context);
    }

    // Handle rate limiting with appropriate delays
    if (error.status === 429) {
      return this.recoverRateLimitStatusError(error, documentId, context);
    }

    // Handle network timeouts
    if (error.name === 'TimeoutError' || error.message?.includes('timeout')) {
      return this.recoverTimeoutStatusError(error, documentId, context);
    }

    // Default recovery - throw the categorized error
    return throwError(() => this.createStatusErrorResponse(error, documentId, context));
  }

  /**
   * Recover from 404 status errors
   */
  private recover404StatusError(
    error: any, 
    documentId: string, 
    context: StatusRequestContext
  ): Observable<DocumentVerificationResult> {
    const isDocumentTracked = this.isDocumentTracked(documentId);
    
    if (!isDocumentTracked) {
      // Permanent 404 - document not found
      console.error(`DocumentService: Permanent 404 for untracked document ${documentId}`);
      return throwError(() => this.createStatusErrorResponse(error, documentId, context));
    }

    // Temporary 404 - document not ready yet
    const waitTime = this.calculateSuggestedWaitTime(error, context) || 5000;
    console.log(`DocumentService: Temporary 404 for document ${documentId}, waiting ${waitTime}ms before retry`);
    
    return throwError(() => {
      const statusError = this.createStatusErrorResponse(error, documentId, context);
      // Add recovery context
      return {
        ...statusError,
        message: `${statusError.message} The system will automatically retry in ${Math.round(waitTime / 1000)} seconds.`,
        recoveryAction: 'automatic_retry',
        recoveryDelay: waitTime
      };
    });
  }

  /**
   * Recover from server status errors
   */
  private recoverServerStatusError(
    error: any, 
    documentId: string, 
    context: StatusRequestContext
  ): Observable<DocumentVerificationResult> {
    const waitTime = this.calculateSuggestedWaitTime(error, context) || 10000;
    console.log(`DocumentService: Server error for document ${documentId}, waiting ${waitTime}ms before retry`);
    
    return throwError(() => {
      const statusError = this.createStatusErrorResponse(error, documentId, context);
      return {
        ...statusError,
        message: `${statusError.message} Retrying automatically in ${Math.round(waitTime / 1000)} seconds.`,
        recoveryAction: 'automatic_retry',
        recoveryDelay: waitTime
      };
    });
  }

  /**
   * Recover from rate limit status errors
   */
  private recoverRateLimitStatusError(
    error: any, 
    documentId: string, 
    context: StatusRequestContext
  ): Observable<DocumentVerificationResult> {
    // Use longer wait time for rate limiting
    const waitTime = Math.max(30000, this.calculateSuggestedWaitTime(error, context) || 30000);
    console.log(`DocumentService: Rate limit error for document ${documentId}, waiting ${waitTime}ms before retry`);
    
    return throwError(() => {
      const statusError = this.createStatusErrorResponse(error, documentId, context);
      return {
        ...statusError,
        message: 'Too many status requests. The system will wait before retrying automatically.',
        recoveryAction: 'automatic_retry',
        recoveryDelay: waitTime
      };
    });
  }

  /**
   * Recover from timeout status errors
   */
  private recoverTimeoutStatusError(
    error: any, 
    documentId: string, 
    context: StatusRequestContext
  ): Observable<DocumentVerificationResult> {
    const waitTime = this.calculateSuggestedWaitTime(error, context) || 5000;
    console.log(`DocumentService: Timeout error for document ${documentId}, waiting ${waitTime}ms before retry`);
    
    return throwError(() => {
      const statusError = this.createStatusErrorResponse(error, documentId, context);
      return {
        ...statusError,
        message: 'Status request timed out. The system will retry automatically with a longer timeout.',
        recoveryAction: 'automatic_retry',
        recoveryDelay: waitTime
      };
    });
  }

  /**
   * Create user-friendly error messages for status retrieval failures
   * Public method for components to get appropriate error messages
   */
  public createUserFriendlyStatusErrorMessage(
    error: any, 
    documentId: string, 
    context?: StatusRequestContext
  ): string {
    // If no context provided, create minimal context
    const requestContext = context || {
      documentId,
      documentType: 'unknown',
      uploadTimestamp: Date.now() - 60000, // Assume 1 minute ago
      retryCount: 0,
      lastAttemptTimestamp: Date.now(),
      maxRetries: 10,
      pollingStartTime: Date.now() - 60000
    };

    const timeSinceUpload = Date.now() - requestContext.uploadTimestamp;
    const isDocumentTracked = this.isDocumentTracked(documentId);

    if (error.status === 404) {
      if (!isDocumentTracked) {
        return 'Document not found. Please verify the document was uploaded successfully.';
      }
      
      if (timeSinceUpload < 30000) {
        return 'Your document is being processed. Status will be available shortly.';
      }
      
      if (timeSinceUpload < 120000) {
        return 'Document verification is in progress. Please wait a bit longer.';
      }
      
      return 'Document verification is taking longer than usual. Please continue waiting or try refreshing the page.';
    }

    if (error.status >= 500) {
      return 'The verification service is temporarily unavailable. The system will keep trying automatically.';
    }

    if (error.status === 429) {
      return 'Please wait - the system is managing the request rate automatically.';
    }

    if (error.name === 'TimeoutError') {
      return 'The status request is taking longer than expected. The system will retry automatically.';
    }

    return 'Unable to retrieve document status. The system will continue trying automatically.';
  }

  /**
   * Get recovery recommendations for status errors
   * Public method for components to understand what actions are being taken
   */
  public getStatusErrorRecoveryInfo(error: any, documentId: string): {
    isRecoverable: boolean;
    recoveryType: 'automatic' | 'manual' | 'none';
    userAction?: string;
    estimatedWaitTime?: number;
  } {
    const isDocumentTracked = this.isDocumentTracked(documentId);

    if (error.status === 404) {
      if (!isDocumentTracked) {
        return {
          isRecoverable: false,
          recoveryType: 'manual',
          userAction: 'Please re-upload the document or verify the document ID is correct.'
        };
      }
      
      return {
        isRecoverable: true,
        recoveryType: 'automatic',
        userAction: 'The system will automatically retry. Please wait while your document is being processed.',
        estimatedWaitTime: 30000 // 30 seconds
      };
    }

    if (error.status >= 500) {
      return {
        isRecoverable: true,
        recoveryType: 'automatic',
        userAction: 'The system will automatically retry with increasing delays.',
        estimatedWaitTime: 10000 // 10 seconds
      };
    }

    if (error.status === 429) {
      return {
        isRecoverable: true,
        recoveryType: 'automatic',
        userAction: 'The system will wait and retry automatically to respect rate limits.',
        estimatedWaitTime: 30000 // 30 seconds
      };
    }

    if (error.name === 'TimeoutError') {
      return {
        isRecoverable: true,
        recoveryType: 'automatic',
        userAction: 'The system will retry with a longer timeout.',
        estimatedWaitTime: 5000 // 5 seconds
      };
    }

    return {
      isRecoverable: false,
      recoveryType: 'manual',
      userAction: 'Please try refreshing the page or re-uploading the document.'
    };
  }

  /**
   * Create status-specific error response with enhanced categorization
   */
  private createStatusErrorResponse(
    error: any, 
    documentId: string, 
    context: StatusRequestContext
  ): StatusErrorResponse {
    const statusError = this.categorizeStatusError(error, documentId, context);
    
    return {
      ...statusError,
      documentId,
      documentExists: this.isDocumentTracked(documentId),
      suggestedWaitTime: this.calculateSuggestedWaitTime(error, context),
      retryRecommended: this.isStatusErrorRetryable(error, context)
    };
  }

  /**
   * Categorize errors specifically for status requests
   * Distinguishes between different types of 404 errors and provides context-aware responses
   */
  private categorizeStatusError(error: any, documentId: string, context: StatusRequestContext): ErrorResponse {
    // Handle 404 errors with specific categorization for status requests
    if (error.status === 404) {
      return this.categorize404StatusError(error, documentId, context);
    }

    // Handle other HTTP status codes with status-specific context
    if (error.status) {
      if (error.status >= 500) {
        return {
          type: 'server',
          message: 'Server error occurred while checking document status. The verification service may be temporarily unavailable.',
          actionable: true,
          suggestedAction: 'Wait a moment and the system will automatically retry checking the status.',
          originalError: error
        };
      } else if (error.status === 429) {
        return {
          type: 'network',
          message: 'Too many status requests. Please wait before checking again.',
          actionable: true,
          suggestedAction: 'The system will automatically retry with appropriate delays.',
          originalError: error
        };
      } else if (error.status >= 400 && error.status < 500) {
        return {
          type: 'network',
          message: 'Unable to retrieve document status. There may be an issue with the request.',
          actionable: true,
          suggestedAction: 'The system will retry automatically. If the issue persists, try re-uploading the document.',
          originalError: error
        };
      }
    }

    // Network connectivity errors for status requests
    if (error.name === 'TimeoutError' || error.message?.includes('timeout')) {
      return {
        type: 'network',
        message: 'Status request timed out. The server may be busy processing your document.',
        actionable: true,
        suggestedAction: 'The system will automatically retry. Please wait while your document is being processed.',
        originalError: error
      };
    }

    // Default error for status requests
    return {
      type: 'server',
      message: 'Unable to check document verification status.',
      actionable: true,
      suggestedAction: 'The system will continue trying to retrieve the status. If this persists, try refreshing the page.',
      originalError: error
    };
  }

  /**
   * Categorize 404 errors specifically for status requests
   * Distinguishes between "document not found" vs "document not ready" scenarios
   */
  private categorize404StatusError(error: any, documentId: string, context: StatusRequestContext): ErrorResponse {
    const isDocumentTracked = this.isDocumentTracked(documentId);
    const timeSinceUpload = Date.now() - context.uploadTimestamp;
    const timeSincePollingStart = Date.now() - context.pollingStartTime;

    // Document not found - permanent 404
    if (!isDocumentTracked) {
      return {
        type: 'network',
        message: 'Document not found. The document ID may be invalid or the document was not uploaded successfully.',
        actionable: true,
        suggestedAction: 'Please verify the document was uploaded correctly or try uploading again.',
        originalError: error
      };
    }

    // Document exists but status not ready - temporary 404
    if (timeSinceUpload < 10000) { // Less than 10 seconds since upload
      return {
        type: 'network',
        message: 'Document is still being processed. Status information is not yet available.',
        actionable: true,
        suggestedAction: 'Please wait while the document is being analyzed. This usually takes a few moments.',
        originalError: error
      };
    }

    // Document processing may be taking longer than expected
    if (timeSinceUpload < 60000) { // Less than 1 minute since upload
      return {
        type: 'network',
        message: 'Document verification is in progress. Status will be available shortly.',
        actionable: true,
        suggestedAction: 'The document is being processed. Please wait a bit longer for the verification to complete.',
        originalError: error
      };
    }

    // Long processing time - may indicate an issue
    if (timeSinceUpload < 300000) { // Less than 5 minutes since upload
      return {
        type: 'network',
        message: 'Document verification is taking longer than usual. This may be due to high server load or document complexity.',
        actionable: true,
        suggestedAction: 'Please continue waiting. Complex documents may take several minutes to process.',
        originalError: error
      };
    }

    // Very long processing time - likely an issue
    return {
      type: 'server',
      message: 'Document verification appears to be stuck. There may be an issue with the processing service.',
      actionable: true,
      suggestedAction: 'Consider refreshing the page or re-uploading the document if the status doesn\'t update soon.',
      originalError: error
    };
  }

  /**
   * Calculate suggested wait time based on error type and context
   */
  private calculateSuggestedWaitTime(error: any, context: StatusRequestContext): number | undefined {
    if (error.status === 404) {
      const timeSinceUpload = Date.now() - context.uploadTimestamp;
      
      // Recent upload - short wait
      if (timeSinceUpload < 10000) {
        return 2000; // 2 seconds
      }
      
      // Medium wait for processing
      if (timeSinceUpload < 60000) {
        return 5000; // 5 seconds
      }
      
      // Longer wait for complex processing
      return 10000; // 10 seconds
    }

    // Server errors - exponential backoff based on retry count
    if (error.status >= 500) {
      return Math.min(2000 * Math.pow(2, context.retryCount), 30000); // Cap at 30 seconds
    }

    // Network errors
    if (error.name === 'TimeoutError') {
      return 5000; // 5 seconds for timeout
    }

    return undefined;
  }

  /**
   * Determine if a status error should be retried
   */
  private isStatusErrorRetryable(error: any, context: StatusRequestContext): boolean {
    // Always retry 404 errors for status requests (document may not be ready)
    if (error.status === 404) {
      return true;
    }

    // Retry server errors
    if (error.status >= 500) {
      return true;
    }

    // Retry network timeouts
    if (error.name === 'TimeoutError' || error.message?.includes('timeout')) {
      return true;
    }

    // Don't retry client errors (except 404)
    if (error.status >= 400 && error.status < 500) {
      return false;
    }

    // Retry unknown errors (may be network issues)
    return true;
  }

  /**
   * Validate document ID format (GUID)
   * Enhanced validation with detailed error reporting
   */
  private isValidDocumentId(documentId: string): boolean {
    if (!documentId || typeof documentId !== 'string') {
      return false;
    }

    const trimmedId = documentId.trim();
    
    // Check for empty string after trimming
    if (!trimmedId) {
      return false;
    }

    // GUID format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return guidRegex.test(trimmedId);
  }

  /**
   * Validate document ID format with detailed error information
   * Public method for external validation needs
   */
  public validateDocumentIdFormat(documentId: string): { isValid: boolean; error?: string } {
    if (!documentId) {
      return {
        isValid: false,
        error: 'Document ID is required and cannot be null or undefined.'
      };
    }

    if (typeof documentId !== 'string') {
      return {
        isValid: false,
        error: 'Document ID must be a string.'
      };
    }

    const trimmedId = documentId.trim();
    
    if (!trimmedId) {
      return {
        isValid: false,
        error: 'Document ID cannot be empty or contain only whitespace.'
      };
    }

    // Check length (GUID should be 36 characters with hyphens)
    if (trimmedId.length !== 36) {
      return {
        isValid: false,
        error: `Document ID must be 36 characters long (GUID format). Received: ${trimmedId.length} characters.`
      };
    }

    // Check GUID format pattern
    const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (!guidRegex.test(trimmedId)) {
      return {
        isValid: false,
        error: 'Document ID must be in valid GUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).'
      };
    }

    return { isValid: true };
  }

  /**
   * Validate that document was successfully uploaded
   */
  private isDocumentUploaded(documentId: string): boolean {
    return this.uploadedDocumentIds.has(documentId);
  }

  /**
   * Check if document exists in uploaded documents tracking
   * Public method for external existence checks
   */
  public isDocumentTracked(documentId: string): boolean {
    if (!this.isValidDocumentId(documentId)) {
      return false;
    }
    return this.isDocumentUploaded(documentId);
  }

  /**
   * Validate document ID before making status requests
   * Enhanced with comprehensive validation and logging
   */
  private validateDocumentForStatusRequest(documentId: string): { isValid: boolean; error?: string } {
    console.log(`DocumentService: Validating document ID for status request: ${documentId}`);
    
    // First check document ID format
    const formatValidation = this.validateDocumentIdFormat(documentId);
    if (!formatValidation.isValid) {
      console.warn(`DocumentService: Document ID format validation failed:`, formatValidation.error);
      return formatValidation;
    }

    // Check if document was successfully uploaded
    if (!this.isDocumentUploaded(documentId)) {
      const error = 'Document not found in uploaded documents. The document may not have been uploaded successfully or the ID is incorrect.';
      console.warn(`DocumentService: Document existence validation failed:`, {
        documentId,
        trackedDocuments: Array.from(this.uploadedDocumentIds),
        error
      });
      return {
        isValid: false,
        error
      };
    }

    console.log(`DocumentService: Document ID validation successful for: ${documentId}`);
    return { isValid: true };
  }

  /**
   * Add document ID to uploaded documents tracking
   */
  private trackUploadedDocument(documentId: string): void {
    if (this.isValidDocumentId(documentId)) {
      this.uploadedDocumentIds.add(documentId);
      console.log(`DocumentService: Tracking uploaded document ${documentId}`);
    }
  }

  /**
   * Remove document ID from uploaded documents tracking
   */
  private untrackUploadedDocument(documentId: string): void {
    this.uploadedDocumentIds.delete(documentId);
    console.log(`DocumentService: Stopped tracking document ${documentId}`);
  }

  /**
   * Get all tracked document IDs
   * Public method for debugging and state inspection
   */
  public getTrackedDocumentIds(): string[] {
    return Array.from(this.uploadedDocumentIds);
  }

  /**
   * Clear all tracked document IDs
   * Useful for testing or when user logs out
   */
  public clearTrackedDocuments(): void {
    const count = this.uploadedDocumentIds.size;
    this.uploadedDocumentIds.clear();
    console.log(`DocumentService: Cleared ${count} tracked documents`);
  }

  /**
   * Validate document exists and is ready for status requests
   * Enhanced pre-check method with detailed validation
   */
  public validateDocumentForStatusCheck(documentId: string): { 
    canCheckStatus: boolean; 
    error?: string; 
    validationDetails: {
      hasValidFormat: boolean;
      isTracked: boolean;
      documentId: string;
    }
  } {
    const formatValidation = this.validateDocumentIdFormat(documentId);
    const isTracked = this.isDocumentTracked(documentId);

    const validationDetails = {
      hasValidFormat: formatValidation.isValid,
      isTracked,
      documentId
    };

    if (!formatValidation.isValid) {
      return {
        canCheckStatus: false,
        error: formatValidation.error,
        validationDetails
      };
    }

    if (!isTracked) {
      return {
        canCheckStatus: false,
        error: 'Document not found in uploaded documents. Please ensure the document was uploaded successfully before checking status.',
        validationDetails
      };
    }

    return {
      canCheckStatus: true,
      validationDetails
    };
  }

  /**
   * Generate unique request ID for tracking
   */
  private generateRequestId(): string {
    return `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  }

  /**
   * Log API request details
   */
  private logApiRequest(method: string, url: string, requestId: string, requestBody?: any, file?: File): void {
    const logEntry: ApiRequestLog = {
      timestamp: new Date().toISOString(),
      method: method.toUpperCase(),
      url,
      requestId,
      requestBody: requestBody ? this.sanitizeRequestBody(requestBody) : undefined,
      fileInfo: file ? {
        name: file.name,
        size: file.size,
        type: file.type
      } : undefined
    };

    console.log('DocumentService API Request:', logEntry);
    
    // Store in session storage for debugging (limit to last 50 requests)
    this.storeRequestLog(logEntry);
  }

  /**
   * Log API response details
   */
  private logApiResponse(requestId: string, status: number, statusText: string, responseBody?: any, startTime?: number): void {
    const duration = startTime ? Date.now() - startTime : 0;
    const logEntry: ApiResponseLog = {
      timestamp: new Date().toISOString(),
      requestId,
      status,
      statusText,
      responseBody: responseBody ? this.sanitizeResponseBody(responseBody) : undefined,
      duration,
      success: status >= 200 && status < 300
    };

    console.log('DocumentService API Response:', logEntry);
    
    // Store in session storage for debugging
    this.storeResponseLog(logEntry);
  }

  /**
   * Log API error details with comprehensive context
   */
  private logApiError(
    requestId: string, 
    method: string, 
    url: string, 
    error: any, 
    context: { operation: string; documentType?: string; fileSize?: number; retryAttempt?: number },
    startTime?: number
  ): void {
    const duration = startTime ? Date.now() - startTime : 0;
    const logEntry: ApiErrorLog = {
      timestamp: new Date().toISOString(),
      requestId,
      method: method.toUpperCase(),
      url,
      status: error.status,
      statusText: error.statusText,
      errorMessage: error.message || 'Unknown error',
      errorDetails: {
        error: error.error,
        name: error.name,
        stack: error.stack
      },
      duration,
      context
    };

    console.error('DocumentService API Error:', logEntry);
    
    // Store in session storage for debugging
    this.storeErrorLog(logEntry);

    // Log additional context for 404 errors
    if (error.status === 404) {
      console.error('DocumentService 404 Error Details:', {
        attemptedUrl: url,
        method: method.toUpperCase(),
        baseUrl: this.baseUrl,
        fullContext: context,
        possibleCauses: [
          'API endpoint URL mismatch',
          'Backend server not running',
          'Incorrect base URL configuration',
          'Route configuration issue'
        ],
        troubleshooting: {
          checkBaseUrl: this.baseUrl,
          expectedEndpoint: '/api/Document/upload',
          currentAttempt: url
        }
      });
    }
  }

  /**
   * Log status request details with enhanced context for polling lifecycle
   */
  private logStatusRequest(
    operation: StatusRequestLog['operation'],
    documentId: string,
    documentType: string,
    requestId: string,
    url: string,
    additionalContext?: {
      retryCount?: number;
      backoffInterval?: number;
      totalElapsedTime?: number;
      pollingSessionId?: string;
      result?: StatusRequestLog['result'];
      errorDetails?: StatusRequestLog['errorDetails'];
      pollingContext?: StatusRequestLog['pollingContext'];
    }
  ): void {
    const logEntry: StatusRequestLog = {
      timestamp: new Date().toISOString(),
      requestId,
      documentId,
      documentType,
      operation,
      url,
      ...additionalContext
    };

    console.log('DocumentService Status Request:', logEntry);
    
    // Store in session storage for debugging
    this.storeStatusRequestLog(logEntry);

    // Log detailed context for specific operations
    switch (operation) {
      case 'status_polling_start':
        console.log(`DocumentService: Starting status polling session for document ${documentId}:`, {
          documentType,
          pollingConfig: this.defaultPollingConfig,
          url,
          requestId
        });
        break;
        
      case 'status_polling_retry':
        console.log(`DocumentService: Status polling retry ${additionalContext?.retryCount || 0} for document ${documentId}:`, {
          backoffInterval: additionalContext?.backoffInterval ? `${additionalContext.backoffInterval}ms` : 'unknown',
          totalElapsed: additionalContext?.totalElapsedTime ? `${additionalContext.totalElapsedTime}ms` : 'unknown',
          nextAttemptTime: additionalContext?.backoffInterval ? 
            new Date(Date.now() + additionalContext.backoffInterval).toISOString() : 'unknown'
        });
        break;
        
      case 'status_polling_success':
        console.log(`DocumentService: Status polling successful for document ${documentId}:`, {
          totalRetries: additionalContext?.retryCount || 0,
          totalDuration: additionalContext?.totalElapsedTime ? `${additionalContext.totalElapsedTime}ms` : 'unknown',
          requestId
        });
        break;
        
      case 'status_polling_failure':
        console.error(`DocumentService: Status polling failed for document ${documentId}:`, {
          error: additionalContext?.errorDetails,
          totalRetries: additionalContext?.retryCount || 0,
          totalDuration: additionalContext?.totalElapsedTime ? `${additionalContext.totalElapsedTime}ms` : 'unknown',
          requestId
        });
        break;
        
      case 'status_polling_timeout':
        console.warn(`DocumentService: Status polling timeout for document ${documentId}:`, {
          totalRetries: additionalContext?.retryCount || 0,
          totalDuration: additionalContext?.totalElapsedTime ? `${additionalContext.totalElapsedTime}ms` : 'unknown',
          timeoutMs: additionalContext?.pollingContext?.timeoutMs || this.defaultPollingConfig.timeoutMs,
          requestId
        });
        break;
    }
  }

  /**
   * Sanitize request body for logging (remove sensitive data)
   */
  private sanitizeRequestBody(body: any): any {
    if (!body) return body;
    
    // Create a copy to avoid modifying original
    const sanitized = { ...body };
    
    // Remove or mask sensitive fields
    if (sanitized.password) sanitized.password = '[REDACTED]';
    if (sanitized.token) sanitized.token = '[REDACTED]';
    if (sanitized.apiKey) sanitized.apiKey = '[REDACTED]';
    
    return sanitized;
  }

  /**
   * Sanitize response body for logging (remove sensitive data)
   */
  private sanitizeResponseBody(body: any): any {
    if (!body) return body;
    
    // Create a copy to avoid modifying original
    const sanitized = { ...body };
    
    // Remove or mask sensitive fields
    if (sanitized.token) sanitized.token = '[REDACTED]';
    if (sanitized.apiKey) sanitized.apiKey = '[REDACTED]';
    
    return sanitized;
  }

  /**
   * Store request log in session storage
   */
  private storeRequestLog(logEntry: ApiRequestLog): void {
    try {
      const key = 'documentService_requestLogs';
      const existing = JSON.parse(sessionStorage.getItem(key) || '[]');
      existing.push(logEntry);
      
      // Keep only last 50 entries
      if (existing.length > 50) {
        existing.splice(0, existing.length - 50);
      }
      
      sessionStorage.setItem(key, JSON.stringify(existing));
    } catch (error) {
      console.warn('Failed to store request log:', error);
    }
  }

  /**
   * Store response log in session storage
   */
  private storeResponseLog(logEntry: ApiResponseLog): void {
    try {
      const key = 'documentService_responseLogs';
      const existing = JSON.parse(sessionStorage.getItem(key) || '[]');
      existing.push(logEntry);
      
      // Keep only last 50 entries
      if (existing.length > 50) {
        existing.splice(0, existing.length - 50);
      }
      
      sessionStorage.setItem(key, JSON.stringify(existing));
    } catch (error) {
      console.warn('Failed to store response log:', error);
    }
  }

  /**
   * Store error log in session storage
   */
  private storeErrorLog(logEntry: ApiErrorLog): void {
    try {
      const key = 'documentService_errorLogs';
      const existing = JSON.parse(sessionStorage.getItem(key) || '[]');
      existing.push(logEntry);
      
      // Keep only last 50 entries
      if (existing.length > 50) {
        existing.splice(0, existing.length - 50);
      }
      
      sessionStorage.setItem(key, JSON.stringify(existing));
    } catch (error) {
      console.warn('Failed to store error log:', error);
    }
  }

  /**
   * Store status request log in session storage
   */
  private storeStatusRequestLog(logEntry: StatusRequestLog): void {
    try {
      const key = 'documentService_statusRequestLogs';
      const existing = JSON.parse(sessionStorage.getItem(key) || '[]');
      existing.push(logEntry);
      
      // Keep only last 100 entries (status requests are frequent)
      if (existing.length > 100) {
        existing.splice(0, existing.length - 100);
      }
      
      sessionStorage.setItem(key, JSON.stringify(existing));
    } catch (error) {
      console.warn('Failed to store status request log:', error);
    }
  }

  /**
   * Get stored logs for debugging
   */
  getStoredLogs(): { 
    requests: ApiRequestLog[]; 
    responses: ApiResponseLog[]; 
    errors: ApiErrorLog[];
    statusRequests: StatusRequestLog[];
  } {
    try {
      return {
        requests: JSON.parse(sessionStorage.getItem('documentService_requestLogs') || '[]'),
        responses: JSON.parse(sessionStorage.getItem('documentService_responseLogs') || '[]'),
        errors: JSON.parse(sessionStorage.getItem('documentService_errorLogs') || '[]'),
        statusRequests: JSON.parse(sessionStorage.getItem('documentService_statusRequestLogs') || '[]')
      };
    } catch (error) {
      console.warn('Failed to retrieve stored logs:', error);
      return { requests: [], responses: [], errors: [], statusRequests: [] };
    }
  }

  /**
   * Clear stored logs
   */
  clearStoredLogs(): void {
    try {
      sessionStorage.removeItem('documentService_requestLogs');
      sessionStorage.removeItem('documentService_responseLogs');
      sessionStorage.removeItem('documentService_errorLogs');
      sessionStorage.removeItem('documentService_statusRequestLogs');
      sessionStorage.removeItem('documentService_pollingStateEvents');
    } catch (error) {
      console.warn('Failed to clear stored logs:', error);
    }
  }

  /**
   * Get status request logs for specific document
   */
  getStatusRequestLogsForDocument(documentId: string): StatusRequestLog[] {
    try {
      const allStatusLogs = JSON.parse(sessionStorage.getItem('documentService_statusRequestLogs') || '[]');
      return allStatusLogs.filter((log: StatusRequestLog) => log.documentId === documentId);
    } catch (error) {
      console.warn('Failed to retrieve status request logs for document:', error);
      return [];
    }
  }

  /**
   * Get polling statistics for a document
   */
  getPollingStatistics(documentId: string): {
    totalRequests: number;
    successfulRequests: number;
    failedRequests: number;
    retryCount: number;
    averageResponseTime: number;
    pollingDuration: number;
    lastActivity: string;
  } {
    const logs = this.getStatusRequestLogsForDocument(documentId);
    
    if (logs.length === 0) {
      return {
        totalRequests: 0,
        successfulRequests: 0,
        failedRequests: 0,
        retryCount: 0,
        averageResponseTime: 0,
        pollingDuration: 0,
        lastActivity: 'Never'
      };
    }

    const successfulRequests = logs.filter(log => log.result === 'success').length;
    const failedRequests = logs.filter(log => log.result === 'failure').length;
    const retryRequests = logs.filter(log => log.operation === 'status_polling_retry').length;
    
    const firstLog = logs[0];
    const lastLog = logs[logs.length - 1];
    const pollingDuration = new Date(lastLog.timestamp).getTime() - new Date(firstLog.timestamp).getTime();
    
    // Calculate average response time from successful requests
    const successfulLogs = logs.filter(log => log.result === 'success' && log.totalElapsedTime);
    const averageResponseTime = successfulLogs.length > 0 
      ? successfulLogs.reduce((sum, log) => sum + (log.totalElapsedTime || 0), 0) / successfulLogs.length
      : 0;

    return {
      totalRequests: logs.length,
      successfulRequests,
      failedRequests,
      retryCount: retryRequests,
      averageResponseTime: Math.round(averageResponseTime),
      pollingDuration,
      lastActivity: lastLog.timestamp
    };
  }

  /**
   * Create and track a new polling session state
   */
  private createPollingSessionState(
    documentId: string, 
    documentType: string, 
    sessionId: string, 
    config: StatusPollingConfig
  ): PollingSessionState {
    const sessionState: PollingSessionState = {
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
      lastRequestTime: Date.now(),
      config,
    };

    this.pollingSessionStates.set(documentId, sessionState);
    
    // Log session start event
    this.logPollingStateEvent(sessionId, documentId, 'session_start', {
      totalElapsedTime: 0
    });

    console.log(`DocumentService: Created polling session state for document ${documentId}:`, {
      sessionId,
      config,
      sessionState
    });

    return sessionState;
  }

  /**
   * Update polling session state
   */
  private updatePollingSessionState(
    documentId: string, 
    updates: Partial<PollingSessionState>
  ): void {
    const sessionState = this.pollingSessionStates.get(documentId);
    if (!sessionState) {
      console.warn(`DocumentService: Cannot update polling session state - no session found for document ${documentId}`);
      return;
    }

    // Apply updates
    Object.assign(sessionState, updates);
    
    console.log(`DocumentService: Updated polling session state for document ${documentId}:`, {
      sessionId: sessionState.sessionId,
      updates,
      currentState: sessionState
    });
  }

  /**
   * Get polling session state for a document
   */
  public getPollingSessionState(documentId: string): PollingSessionState | undefined {
    return this.pollingSessionStates.get(documentId);
  }

  /**
   * Get all active polling session states
   */
  public getAllPollingSessionStates(): PollingSessionState[] {
    return Array.from(this.pollingSessionStates.values());
  }

  /**
   * Get active polling session states only
   */
  public getActivePollingSessionStates(): PollingSessionState[] {
    return Array.from(this.pollingSessionStates.values())
      .filter(state => state.status === 'active');
  }

  /**
   * Complete polling session state
   */
  private completePollingSessionState(
    documentId: string, 
    finalStatus: 'completed' | 'failed' | 'timeout' | 'cancelled',
    finalError?: { status?: number; message?: string }
  ): void {
    const sessionState = this.pollingSessionStates.get(documentId);
    if (!sessionState) {
      console.warn(`DocumentService: Cannot complete polling session state - no session found for document ${documentId}`);
      return;
    }

    const endTime = Date.now();
    const totalDuration = endTime - sessionState.startTime;

    this.updatePollingSessionState(documentId, {
      status: finalStatus,
      endTime,
      lastError: finalError ? { ...finalError, timestamp: endTime } : undefined
    });

    // Log session completion event
    this.logPollingStateEvent(sessionState.sessionId, documentId, 
      finalStatus === 'completed' ? 'session_completed' : 
      finalStatus === 'timeout' ? 'session_timeout' : 
      finalStatus === 'cancelled' ? 'session_cancelled' : 'session_completed', {
      totalElapsedTime: totalDuration,
      errorStatus: finalError?.status,
      errorMessage: finalError?.message
    });

    console.log(`DocumentService: Completed polling session for document ${documentId}:`, {
      sessionId: sessionState.sessionId,
      finalStatus,
      totalDuration: `${totalDuration}ms`,
      totalRequests: sessionState.totalRequests,
      successfulRequests: sessionState.successfulRequests,
      failedRequests: sessionState.failedRequests,
      finalError
    });
  }

  /**
   * Log polling state event
   */
  private logPollingStateEvent(
    sessionId: string,
    documentId: string,
    eventType: PollingStateEvent['eventType'],
    details?: PollingStateEvent['details']
  ): void {
    const event: PollingStateEvent = {
      timestamp: new Date().toISOString(),
      sessionId,
      documentId,
      eventType,
      details
    };

    this.pollingStateEvents.push(event);
    
    // Keep only last 500 events to prevent memory issues
    if (this.pollingStateEvents.length > 500) {
      this.pollingStateEvents.splice(0, this.pollingStateEvents.length - 500);
    }

    console.log(`DocumentService: Polling state event [${eventType}] for document ${documentId}:`, {
      sessionId,
      details,
      timestamp: event.timestamp
    });

    // Store in session storage for debugging
    this.storePollingStateEvent(event);
  }

  /**
   * Store polling state event in session storage
   */
  private storePollingStateEvent(event: PollingStateEvent): void {
    try {
      const key = 'documentService_pollingStateEvents';
      const existing = JSON.parse(sessionStorage.getItem(key) || '[]');
      existing.push(event);
      
      // Keep only last 200 events in session storage
      if (existing.length > 200) {
        existing.splice(0, existing.length - 200);
      }
      
      sessionStorage.setItem(key, JSON.stringify(existing));
    } catch (error) {
      console.warn('Failed to store polling state event:', error);
    }
  }

  /**
   * Get polling state events for a document
   */
  public getPollingStateEventsForDocument(documentId: string): PollingStateEvent[] {
    return this.pollingStateEvents.filter(event => event.documentId === documentId);
  }

  /**
   * Get polling state events for a session
   */
  public getPollingStateEventsForSession(sessionId: string): PollingStateEvent[] {
    return this.pollingStateEvents.filter(event => event.sessionId === sessionId);
  }

  /**
   * Clear polling session state for a document
   */
  private clearPollingSessionState(documentId: string): void {
    const sessionState = this.pollingSessionStates.get(documentId);
    if (sessionState) {
      // Clear any active timer
      if (sessionState.timerId) {
        clearTimeout(sessionState.timerId);
      }
      
      this.pollingSessionStates.delete(documentId);
      
      console.log(`DocumentService: Cleared polling session state for document ${documentId}:`, {
        sessionId: sessionState.sessionId,
        finalStatus: sessionState.status,
        totalRequests: sessionState.totalRequests
      });
    }
  }

  /**
   * Clear all polling session states
   */
  public clearAllPollingSessionStates(): void {
    const sessionCount = this.pollingSessionStates.size;
    
    // Clear all timers
    for (const [documentId, sessionState] of this.pollingSessionStates.entries()) {
      if (sessionState.timerId) {
        clearTimeout(sessionState.timerId);
      }
      
      // Log cancellation for active sessions
      if (sessionState.status === 'active') {
        this.logPollingStateEvent(sessionState.sessionId, documentId, 'session_cancelled', {
          totalElapsedTime: Date.now() - sessionState.startTime
        });
      }
    }
    
    this.pollingSessionStates.clear();
    
    console.log(`DocumentService: Cleared ${sessionCount} polling session states`);
  }

  /**
   * Get comprehensive polling debug information
   */
  public getPollingDebugInfo(): {
    activeSessions: PollingSessionState[];
    recentEvents: PollingStateEvent[];
    sessionSummary: {
      totalSessions: number;
      activeSessions: number;
      completedSessions: number;
      failedSessions: number;
      timeoutSessions: number;
      cancelledSessions: number;
    };
  } {
    const allSessions = Array.from(this.pollingSessionStates.values());
    const recentEvents = this.pollingStateEvents.slice(-50); // Last 50 events
    
    const sessionSummary = {
      totalSessions: allSessions.length,
      activeSessions: allSessions.filter(s => s.status === 'active').length,
      completedSessions: allSessions.filter(s => s.status === 'completed').length,
      failedSessions: allSessions.filter(s => s.status === 'failed').length,
      timeoutSessions: allSessions.filter(s => s.status === 'timeout').length,
      cancelledSessions: allSessions.filter(s => s.status === 'cancelled').length
    };

    return {
      activeSessions: allSessions.filter(s => s.status === 'active'),
      recentEvents,
      sessionSummary
    };
  }

  /**
   * Attempt to fix 404 errors by trying alternative endpoints
   */
  private getAlternativeEndpoints(originalUrl: string): string[] {
    const alternatives: string[] = [];
    
    // If the original URL has lowercase 'documents', try uppercase 'Document'
    if (originalUrl.includes('/api/documents/')) {
      alternatives.push(originalUrl.replace('/api/documents/', '/api/Document/'));
    }
    
    // If the original URL has uppercase 'Document', try lowercase 'documents'
    if (originalUrl.includes('/api/Document/')) {
      alternatives.push(originalUrl.replace('/api/Document/', '/api/documents/'));
    }
    
    // Try with different API versions
    if (originalUrl.includes('/api/')) {
      alternatives.push(originalUrl.replace('/api/', '/api/v1/'));
      alternatives.push(originalUrl.replace('/api/', '/api/v2/'));
    }
    
    return alternatives;
  }

  /**
   * Retry API request with alternative endpoints on 404 errors
   */
  private retryWith404Recovery<T>(
    originalMethod: string,
    originalUrl: string,
    requestBody: any,
    requestId: string,
    context: { operation: string; documentType?: string; fileSize?: number; retryAttempt?: number },
    file?: File
  ): Observable<T> {
    const alternatives = this.getAlternativeEndpoints(originalUrl);
    let currentAttempt = 0;
    
    const attemptRequest = (url: string, attemptNumber: number): Observable<T> => {
      const retryRequestId = `${requestId}_retry_${attemptNumber}`;
      const startTime = Date.now();
      
      // Log retry attempt
      this.logApiRequest(originalMethod, url, retryRequestId, requestBody, file);
      console.log(`DocumentService: 404 Recovery attempt ${attemptNumber + 1} for ${context.operation}:`, {
        originalUrl,
        retryUrl: url,
        requestId: retryRequestId
      });
      
      let request$: Observable<any>;
      
      // Create appropriate HTTP request based on method
      switch (originalMethod.toUpperCase()) {
        case 'GET':
          request$ = this.http.get<T>(url);
          break;
        case 'POST':
          if (file) {
            // Handle file upload
            const formData = new FormData();
            if (requestBody.file) formData.append('file', requestBody.file);
            if (requestBody.documentType) formData.append('documentType', requestBody.documentType);
            if (requestBody.formId) formData.append('formId', requestBody.formId);
            
            const uploadRequest = new HttpRequest('POST', url, formData, { reportProgress: true });
            request$ = this.http.request<T>(uploadRequest).pipe(
              map(event => {
                if (event.type === HttpEventType.Response && event.body) {
                  return event.body;
                }
                return null;
              }),
              map(result => result as T)
            );
          } else {
            request$ = this.http.post<T>(url, requestBody || {});
          }
          break;
        case 'DELETE':
          request$ = this.http.delete<T>(url);
          break;
        default:
          return throwError(() => new Error(`Unsupported HTTP method: ${originalMethod}`));
      }
      
      return request$.pipe(
        tap(result => {
          this.logApiResponse(retryRequestId, 200, 'OK', result, startTime);
          console.log(`DocumentService: 404 Recovery successful on attempt ${attemptNumber + 1}:`, {
            originalUrl,
            successfulUrl: url,
            requestId: retryRequestId
          });
        }),
        catchError(error => {
          this.logApiError(retryRequestId, originalMethod, url, error, {
            ...context,
            retryAttempt: attemptNumber + 1
          }, startTime);
          
          // If this is also a 404 and we have more alternatives, try the next one
          if (error.status === 404 && attemptNumber < alternatives.length - 1) {
            console.log(`DocumentService: 404 Recovery attempt ${attemptNumber + 1} failed, trying next alternative`);
            return attemptRequest(alternatives[attemptNumber + 1], attemptNumber + 1);
          }
          
          // If all alternatives failed or this is not a 404, throw the error
          console.error(`DocumentService: 404 Recovery failed after ${attemptNumber + 1} attempts:`, {
            originalUrl,
            failedUrl: url,
            allAttemptedUrls: [originalUrl, ...alternatives.slice(0, attemptNumber + 1)],
            finalError: error
          });
          
          return throwError(() => ({
            ...error,
            requestId: retryRequestId,
            recoveryAttempted: true,
            attemptedUrls: [originalUrl, ...alternatives.slice(0, attemptNumber + 1)]
          }));
        })
      );
    };
    
    // If no alternatives available, return the original error
    if (alternatives.length === 0) {
      console.warn(`DocumentService: No alternative endpoints available for 404 recovery:`, {
        originalUrl,
        context
      });
      return throwError(() => new Error('No alternative endpoints available for 404 recovery'));
    }
    
    // Start with the first alternative
    return attemptRequest(alternatives[0], 0);
  }

  /**
   * Handle 404 errors with automatic recovery
   */
  private handle404Error<T>(
    error: any,
    originalMethod: string,
    originalUrl: string,
    requestBody: any,
    requestId: string,
    context: { operation: string; documentType?: string; fileSize?: number },
    file?: File
  ): Observable<T> {
    if (error.status === 404) {
      console.log(`DocumentService: 404 error detected, attempting recovery:`, {
        originalUrl,
        context,
        requestId
      });
      
      return this.retryWith404Recovery<T>(
        originalMethod,
        originalUrl,
        requestBody,
        requestId,
        context,
        file
      );
    }
    
    // Not a 404 error, just rethrow
    return throwError(() => error);
  }

  /**
   * Show user notification for persistent 404 errors
   */
  private notifyUser404Error(context: { operation: string; attemptedUrls: string[] }): void {
    const message = `API endpoint configuration issue detected. Operation: ${context.operation}`;
    const details = `Attempted URLs: ${context.attemptedUrls.join(', ')}`;
    
    console.error('DocumentService: Persistent 404 error - notifying user:', {
      message,
      details,
      context
    });
    
    // You could integrate with a notification service here
    // For now, we'll just log the error details
    alert(`${message}\n\nPlease contact support if this issue persists.\n\nTechnical details:\n${details}`);
  }

  /**
   * Categorize and map errors to user-friendly messages
   */
  private categorizeError(error: any): ErrorResponse {
    // Network errors (404, 500, timeout, etc.)
    if (error.status) {
      if (error.status === 404) {
        return {
          type: 'network',
          message: 'API endpoint not found. Please check server configuration.',
          actionable: true,
          suggestedAction: 'Contact support if this issue persists.',
          originalError: error
        };
      } else if (error.status >= 500) {
        return {
          type: 'server',
          message: 'Server error occurred. Please try again later.',
          actionable: true,
          suggestedAction: 'Wait a moment and try uploading again.',
          originalError: error
        };
      } else if (error.status === 413) {
        return {
          type: 'azure_intelligence',
          message: 'File size exceeds the maximum allowed limit.',
          actionable: true,
          suggestedAction: 'Please upload a smaller file or compress your document.',
          originalError: error
        };
      } else if (error.status >= 400 && error.status < 500) {
        // Check if this is an Azure Intelligence specific error
        if (this.isAzureIntelligenceError(error)) {
          return this.mapAzureIntelligenceError(error);
        }
        
        return {
          type: 'network',
          message: error.error?.message || 'Request failed. Please check your input and try again.',
          actionable: true,
          suggestedAction: 'Verify your document meets the requirements and try again.',
          originalError: error
        };
      }
    }

    // Network connectivity errors
    if (error.name === 'TimeoutError' || error.message?.includes('timeout')) {
      return {
        type: 'network',
        message: 'Request timed out. Please check your connection and try again.',
        actionable: true,
        suggestedAction: 'Check your internet connection and retry the upload.',
        originalError: error
      };
    }

    // Security-related errors
    if (error.message?.includes('security') || error.message?.includes('malicious')) {
      return {
        type: 'security',
        message: 'Document failed security validation.',
        actionable: true,
        suggestedAction: 'Please ensure your document is safe and try uploading a different file.',
        originalError: error
      };
    }

    // Default server error
    return {
      type: 'server',
      message: error.error?.message || error.message || 'An unexpected error occurred.',
      actionable: false,
      suggestedAction: 'Please try again or contact support if the issue persists.',
      originalError: error
    };
  }

  /**
   * Check if error is from Azure Intelligence service
   */
  private isAzureIntelligenceError(error: any): boolean {
    const errorBody = error.error;
    return errorBody && (
      errorBody.source === 'azure_intelligence' ||
      errorBody.code?.startsWith('AI_') ||
      errorBody.service === 'document_intelligence' ||
      errorBody.message?.includes('Azure Intelligence') ||
      errorBody.message?.includes('document analysis') ||
      errorBody.message?.includes('confidence score')
    );
  }

  /**
   * Map Azure Intelligence specific errors to user-friendly messages
   */
  private mapAzureIntelligenceError(error: any): ErrorResponse {
    const errorBody = error.error as AzureIntelligenceError;
    const code = errorBody.code || '';
    const details = errorBody.details || {};

    switch (code) {
      case 'AI_FILE_TOO_LARGE':
        return {
          type: 'azure_intelligence',
          message: `File size (${this.formatFileSize(details.fileSize || 0)}) exceeds Azure Intelligence limit of ${this.formatFileSize(details.maxAllowedSize || 0)}.`,
          actionable: true,
          suggestedAction: 'Please compress your document or upload a smaller file.',
          originalError: error
        };

      case 'AI_UNSUPPORTED_FORMAT':
        return {
          type: 'azure_intelligence',
          message: 'Document format is not supported by Azure Intelligence.',
          actionable: true,
          suggestedAction: 'Please convert your document to PDF, JPEG, or PNG format.',
          originalError: error
        };

      case 'AI_POOR_QUALITY':
        return {
          type: 'azure_intelligence',
          message: 'Document quality is too poor for analysis.',
          actionable: true,
          suggestedAction: 'Please upload a clearer, higher-resolution image of your document.',
          originalError: error
        };

      case 'AI_CONTENT_UNREADABLE':
        return {
          type: 'azure_intelligence',
          message: 'Document content could not be read or analyzed.',
          actionable: true,
          suggestedAction: 'Ensure the document is not corrupted and text is clearly visible.',
          originalError: error
        };

      case 'AI_WRONG_DOCUMENT_TYPE':
        return {
          type: 'azure_intelligence',
          message: `Expected ${details.documentType} but detected a different document type.`,
          actionable: true,
          suggestedAction: 'Please upload the correct type of document or verify the document category.',
          originalError: error
        };

      case 'AI_LOW_CONFIDENCE':
        return {
          type: 'azure_intelligence',
          message: `Document analysis confidence (${details.confidenceScore}%) is too low for automatic verification.`,
          actionable: true,
          suggestedAction: 'Please upload a clearer image or manually confirm if the document is correct.',
          originalError: error
        };

      case 'AI_SERVICE_UNAVAILABLE':
        return {
          type: 'azure_intelligence',
          message: 'Azure Intelligence service is temporarily unavailable.',
          actionable: true,
          suggestedAction: 'Please try again in a few minutes.',
          originalError: error
        };

      default:
        return {
          type: 'azure_intelligence',
          message: errorBody.message || 'Azure Intelligence rejected the document.',
          actionable: true,
          suggestedAction: details.rejectionReason || 'Please try uploading a different document or contact support.',
          originalError: error
        };
    }
  }

  /**
   * Format file size for display
   */
  private formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  /**
   * Get categorized error information from an error object
   */
  getCategorizedError(error: any): ErrorResponse | null {
    if (error && error.categorizedError) {
      return error.categorizedError;
    }
    return null;
  }

  /**
   * Check if an error is recoverable (can be retried)
   */
  isRecoverableError(error: any): boolean {
    const categorizedError = this.getCategorizedError(error) || this.categorizeError(error);
    
    switch (categorizedError.type) {
      case 'network':
        // Network errors are generally recoverable
        return true;
      case 'server':
        // Server errors might be temporary
        return true;
      case 'azure_intelligence':
        // Some Azure Intelligence errors are recoverable
        const code = error.error?.code || '';
        return ['AI_SERVICE_UNAVAILABLE', 'AI_TIMEOUT'].includes(code);
      case 'security':
        // Security errors are not recoverable
        return false;
      default:
        return false;
    }
  }

  /**
   * Upload document with real-time progress tracking
   */
  uploadDocument(request: DocumentUploadRequest): Observable<DocumentVerificationResult> {
    const stopTimer = this.performanceMonitoring.startTimer('document_upload');
    const uploadUrl = `${this.baseUrl}/api/Document/upload`;
    const requestId = this.generateRequestId();
    const startTime = Date.now();
    
    // Check if demo mode is enabled
    if (environment.demoMode) {
      this.logApiRequest('POST', uploadUrl, requestId, { documentType: request.documentType, formId: request.formId }, request.file);
      return this.simulateDocumentUpload(request).pipe(
        tap(() => {
          stopTimer();
          this.logApiResponse(requestId, 200, 'OK', { documentId: 'demo-' + Date.now() }, startTime);
        })
      );
    }

    const formData = new FormData();
    formData.append('file', request.file);
    formData.append('documentType', request.documentType);
    formData.append('formId', request.formId);

    // Log the API request
    this.logApiRequest('POST', uploadUrl, requestId, { 
      documentType: request.documentType, 
      formId: request.formId 
    }, request.file);

    // Initialize progress tracking
    this.updateUploadProgress(request.documentType, 0, 'uploading', 'Starting upload...');
    
    const uploadRequest = new HttpRequest('POST', uploadUrl, formData, {
      reportProgress: true
    });

    return this.http.request<DocumentVerificationResult>(uploadRequest).pipe(
      map(event => {
        switch (event.type) {
          case HttpEventType.UploadProgress:
            if (event.total) {
              const progress = Math.round(100 * event.loaded / event.total);
              this.updateUploadProgress(request.documentType, progress, 'uploading', 
                `Uploading... ${progress}%`);
              
              // Log progress milestones
              if (progress % 25 === 0) {
                console.log(`DocumentService Upload Progress [${requestId}]: ${progress}%`);
              }
            }
            return null;

          case HttpEventType.Response:
            if (event.body) {
              // Log successful response
              this.logApiResponse(requestId, event.status || 200, event.statusText || 'OK', event.body, startTime);
              
              // Track successfully uploaded document
              this.trackUploadedDocument(event.body.documentId);
              
              this.updateUploadProgress(request.documentType, 100, 'processing', 
                'Upload complete, processing document...');
              
              // Start polling for verification results
              this.startVerificationPolling(event.body.documentId, request.documentType);
              
              return event.body;
            }
            return null;

          default:
            return null;
        }
      }),
      catchError(error => {
        stopTimer();
        
        // Try 404 recovery first
        if (error.status === 404) {
          console.log(`DocumentService: 404 error on upload, attempting recovery for request ${requestId}`);
          
          return this.handle404Error<DocumentVerificationResult>(
            error,
            'POST',
            uploadUrl,
            { 
              file: request.file,
              documentType: request.documentType, 
              formId: request.formId 
            },
            requestId,
            {
              operation: 'document_upload',
              documentType: request.documentType,
              fileSize: request.file.size
            },
            request.file
          ).pipe(
            // If recovery succeeds, handle the successful response
            map(result => {
              this.updateUploadProgress(request.documentType, 100, 'processing', 
                'Upload complete, processing document...');
              
              if (result && (result as any).documentId) {
                // Track successfully uploaded document
                this.trackUploadedDocument((result as any).documentId);
                this.startVerificationPolling((result as any).documentId, request.documentType);
              }
              
              return result;
            }),
            // If recovery fails, handle the final error
            catchError(finalError => {
              const categorizedError = this.categorizeError(finalError);
              
              // Log the final error after recovery attempts
              this.logApiError(requestId, 'POST', uploadUrl, finalError, {
                operation: 'document_upload',
                documentType: request.documentType,
                fileSize: request.file.size
              }, startTime);
              
              this.updateUploadProgress(request.documentType, 0, 'error', categorizedError.message);
              
              // Show user notification for persistent 404 errors
              if (finalError.recoveryAttempted && finalError.attemptedUrls) {
                this.notifyUser404Error({
                  operation: 'document upload',
                  attemptedUrls: finalError.attemptedUrls
                });
              }
              
              const enhancedError = {
                ...finalError,
                categorizedError: categorizedError,
                requestId: requestId
              };
              
              return throwError(() => enhancedError);
            })
          );
        }
        
        // Not a 404 error, handle normally
        const categorizedError = this.categorizeError(error);
        
        // Log the error with comprehensive context
        this.logApiError(requestId, 'POST', uploadUrl, error, {
          operation: 'document_upload',
          documentType: request.documentType,
          fileSize: request.file.size
        }, startTime);
        
        this.updateUploadProgress(request.documentType, 0, 'error', categorizedError.message);
        
        // Attach categorized error info to the original error
        const enhancedError = {
          ...error,
          categorizedError: categorizedError,
          requestId: requestId
        };
        
        return throwError(() => enhancedError);
      }),
      tap(() => stopTimer())
    ).pipe(
      // Filter out null values from progress events
      map(result => result as DocumentVerificationResult)
    );
  }

  /**
   * Poll for document verification results using enhanced polling orchestration
   * Updated to use the new intelligent polling with backoff and enhanced error handling
   */
  private startVerificationPolling(documentId: string, documentType: string): void {
    console.log(`DocumentService: Starting enhanced verification polling for document ${documentId} (type: ${documentType})`);
    
    // Use the new polling orchestration with enhanced configuration
    this.getVerificationStatusWithPolling(documentId, documentType).subscribe({
      next: (result) => {
        console.log(`DocumentService: Verification polling completed for ${documentId}:`, {
          status: result.verificationStatus,
          confidence: result.confidenceScore,
          documentType
        });
        
        // Update progress based on verification status
        if (result.verificationStatus !== 'pending') {
          this.updateUploadProgress(documentType, 100, 'completed', result.message);
          this.verificationUpdatesSubject.next(result);
        } else {
          // This shouldn't happen with the new polling logic, but handle it gracefully
          this.updateUploadProgress(documentType, 100, 'processing', 'Document verification in progress...');
        }
      },
      error: (error) => {
        console.error(`DocumentService: Enhanced verification polling failed for ${documentId}:`, {
          documentId,
          documentType,
          status: error.status,
          statusText: error.statusText,
          error: error.error,
          message: error.message,
          userFriendlyMessage: error.userFriendlyMessage,
          recoveryInfo: error.recoveryInfo,
          requestId: error.requestId
        });
        
        // Use enhanced error handling for better user experience
        let errorMessage = 'Verification failed - please try again';
        
        if (error.userFriendlyMessage) {
          errorMessage = error.userFriendlyMessage;
        } else if (error.statusErrorResponse) {
          errorMessage = error.statusErrorResponse.message;
        } else {
          // Fallback to categorized error
          const categorizedError = this.categorizeError(error);
          errorMessage = categorizedError.message;
        }
        
        this.updateUploadProgress(documentType, 0, 'error', errorMessage);
      }
    });
  }

  /**
   * Orchestrate intelligent status polling with backoff, timeout handling, and maximum retry enforcement
   * This method manages the complete polling lifecycle with enhanced error handling and state management
   */
  getVerificationStatusWithPolling(
    documentId: string, 
    documentType: string = 'unknown',
    pollingConfig?: Partial<StatusPollingConfig>
  ): Observable<DocumentVerificationResult> {
    console.log(`DocumentService: Starting status polling orchestration for document ${documentId}:`, {
      documentType,
      pollingConfig: pollingConfig || 'default'
    });
    
    // Use enhanced polling configuration
    const config = this.getPollingConfig(pollingConfig);
    
    // Validate document before starting polling
    const validation = this.validateDocumentForStatusCheck(documentId);
    if (!validation.canCheckStatus) {
      console.error(`DocumentService: Cannot start polling - validation failed for ${documentId}:`, validation.error);
      
      const validationError = {
        status: 400,
        statusText: 'Bad Request',
        error: {
          message: validation.error,
          code: 'INVALID_DOCUMENT_FOR_POLLING'
        },
        message: validation.error,
        name: 'ValidationError',
        validationDetails: validation.validationDetails
      };
      
      return throwError(() => validationError);
    }
    
    // Check if polling is already active for this document
    if (this.isPollingActive(documentId)) {
      console.warn(`DocumentService: Polling already active for document ${documentId}, returning existing observable`);
      // Could return existing observable or throw error - for now, allow concurrent polling
    }
    
    // Execute status request with graduated retry logic and polling orchestration
    return this.executeStatusRequestWithRetry(documentId, documentType, config).pipe(
      tap(result => {
        console.log(`DocumentService: Status polling orchestration completed successfully for document ${documentId}:`, {
          result,
          documentType,
          pollingConfig: config
        });
      }),
      catchError(error => {
        console.error(`DocumentService: Status polling orchestration failed for document ${documentId}:`, {
          error: error.status || error.name,
          message: error.message,
          documentType,
          pollingConfig: config
        });
        
        // Ensure polling cleanup on error
        this.cleanupDocumentPolling(documentId);
        
        return throwError(() => error);
      })
    );
  }

  /**
   * Get verification status for a document with enhanced error handling
   * Integrates document ID validation, status-specific error categorization, and polling configuration
   */
  getVerificationStatus(documentId: string, documentType: string = 'unknown'): Observable<DocumentVerificationResult> {
    console.log(`DocumentService: Getting verification status for document ${documentId} (type: ${documentType})`);
    
    // Enhanced document ID validation before making status request
    const validation = this.validateDocumentForStatusRequest(documentId);
    if (!validation.isValid) {
      console.error(`DocumentService: Document ID validation failed for ${documentId}:`, validation.error);
      
      // Log validation failure with enhanced context
      this.logStatusRequest('status_request', documentId, documentType, this.generateRequestId(), 
        `${this.baseUrl}/api/Document/${documentId}/status`, {
        result: 'failure',
        errorDetails: {
          errorMessage: validation.error || 'Document ID validation failed',
          errorType: 'ValidationError'
        }
      });
      
      // Create status-specific error response for validation failure
      const validationError = {
        status: 400,
        statusText: 'Bad Request',
        error: {
          message: validation.error,
          code: 'INVALID_DOCUMENT_ID'
        },
        message: validation.error,
        name: 'ValidationError'
      };
      
      return throwError(() => validationError);
    }

    const statusUrl = `${this.baseUrl}/api/Document/${documentId}/status`;
    const requestId = this.generateRequestId();
    const startTime = Date.now();

    // Log the status request attempt with enhanced context
    this.logStatusRequest('status_request', documentId, documentType, requestId, statusUrl);

    if (environment.demoMode) {
      this.logApiRequest('GET', statusUrl, requestId, { documentId });
      
      // In demo mode, return a mock result
      const mockResult = {
        documentId,
        verificationStatus: 'verified' as const,
        confidenceScore: 85,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'green' as const,
        message: 'Document verified successfully',
        requiresUserConfirmation: false
      };

      return of(mockResult).pipe(
        delay(500),
        tap(result => {
          this.logApiResponse(requestId, 200, 'OK', result, startTime);
          
          // Log successful status request
          this.logStatusRequest('status_request', documentId, documentType, requestId, statusUrl, {
            result: 'success'
          });
        })
      );
    }

    // Log the API request
    this.logApiRequest('GET', statusUrl, requestId, { documentId });

    return this.http.get<DocumentVerificationResult>(statusUrl).pipe(
      tap(result => {
        this.logApiResponse(requestId, 200, 'OK', result, startTime);
        
        // Log successful status request
        this.logStatusRequest('status_request', documentId, documentType, requestId, statusUrl, {
          result: 'success'
        });
        
        console.log(`DocumentService: Successfully retrieved status for document ${documentId}:`, result);
      }),
      catchError(error => {
        // Log failed status request with enhanced context
        this.logStatusRequest('status_request', documentId, documentType, requestId, statusUrl, {
          result: 'failure',
          errorDetails: {
            status: error.status,
            statusText: error.statusText,
            errorMessage: error.message,
            errorType: error.name
          }
        });
        
        console.error(`DocumentService: Status request failed for document ${documentId}:`, {
          error: error.status,
          message: error.message,
          documentType,
          requestId
        });
        
        // Apply status-specific error categorization and recovery
        const context = this.createStatusRequestContext(documentId, documentType);
        const statusErrorResponse = this.createStatusErrorResponse(error, documentId, context);
        
        // Log the categorized error
        this.logApiError(requestId, 'GET', statusUrl, error, {
          operation: 'get_verification_status',
          documentType: documentType
        }, startTime);
        
        // Return enhanced error with status-specific information
        return throwError(() => ({
          ...error,
          requestId: requestId,
          statusErrorResponse,
          documentId,
          documentType,
          userFriendlyMessage: this.createUserFriendlyStatusErrorMessage(error, documentId, context),
          recoveryInfo: this.getStatusErrorRecoveryInfo(error, documentId)
        }));
      })
    );
  }

  /**
   * Confirm a document with low confidence score
   */
  confirmDocument(documentId: string): Observable<DocumentVerificationResult> {
    const confirmUrl = `${this.baseUrl}/api/Document/${documentId}/confirm`;
    const requestId = this.generateRequestId();
    const startTime = Date.now();

    if (environment.demoMode) {
      this.logApiRequest('POST', confirmUrl, requestId, { documentId });
      
      const result: DocumentVerificationResult = {
        documentId,
        verificationStatus: 'verified' as const,
        confidenceScore: 75,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'green' as const,
        message: 'Document confirmed by user',
        requiresUserConfirmation: false
      };

      return of(result).pipe(
        delay(500),
        tap(result => {
          this.logApiResponse(requestId, 200, 'OK', result, startTime);
          this.verificationUpdatesSubject.next(result);
        })
      );
    }

    // Log the API request
    this.logApiRequest('POST', confirmUrl, requestId, { documentId });

    return this.http.post<DocumentVerificationResult>(confirmUrl, {}).pipe(
      tap(result => {
        this.logApiResponse(requestId, 200, 'OK', result, startTime);
        this.verificationUpdatesSubject.next(result);
      }),
      catchError(error => {
        // Try 404 recovery first
        if (error.status === 404) {
          return this.handle404Error<DocumentVerificationResult>(
            error,
            'POST',
            confirmUrl,
            { documentId },
            requestId,
            {
              operation: 'confirm_document',
              documentType: 'unknown'
            }
          ).pipe(
            tap(result => {
              this.verificationUpdatesSubject.next(result);
            }),
            catchError(finalError => {
              this.logApiError(requestId, 'POST', confirmUrl, finalError, {
                operation: 'confirm_document',
                documentType: 'unknown'
              }, startTime);
              
              // Show user notification for persistent 404 errors
              if (finalError.recoveryAttempted && finalError.attemptedUrls) {
                this.notifyUser404Error({
                  operation: 'confirm document',
                  attemptedUrls: finalError.attemptedUrls
                });
              }
              
              return throwError(() => ({
                ...finalError,
                requestId: requestId
              }));
            })
          );
        }
        
        // Not a 404 error, handle normally
        this.logApiError(requestId, 'POST', confirmUrl, error, {
          operation: 'confirm_document',
          documentType: 'unknown'
        }, startTime);
        
        return throwError(() => ({
          ...error,
          requestId: requestId
        }));
      })
    );
  }

  /**
   * Delete a document and allow re-upload
   */
  deleteDocument(documentId: string): Observable<void> {
    const deleteUrl = `${this.baseUrl}/api/Document/${documentId}`;
    const requestId = this.generateRequestId();
    const startTime = Date.now();

    if (environment.demoMode) {
      this.logApiRequest('DELETE', deleteUrl, requestId, { documentId });
      
      return of(void 0).pipe(
        delay(300),
        tap(() => {
          this.logApiResponse(requestId, 200, 'OK', undefined, startTime);
          // Remove document from tracking when deleted in demo mode
          this.untrackUploadedDocument(documentId);
        })
      );
    }

    // Log the API request
    this.logApiRequest('DELETE', deleteUrl, requestId, { documentId });

    return this.http.delete<void>(deleteUrl).pipe(
      tap(() => {
        this.logApiResponse(requestId, 200, 'OK', undefined, startTime);
        // Remove document from tracking when deleted
        this.untrackUploadedDocument(documentId);
      }),
      catchError(error => {
        // Try 404 recovery first
        if (error.status === 404) {
          return this.handle404Error<void>(
            error,
            'DELETE',
            deleteUrl,
            { documentId },
            requestId,
            {
              operation: 'delete_document',
              documentType: 'unknown'
            }
          ).pipe(
            catchError(finalError => {
              this.logApiError(requestId, 'DELETE', deleteUrl, finalError, {
                operation: 'delete_document',
                documentType: 'unknown'
              }, startTime);
              
              // Show user notification for persistent 404 errors
              if (finalError.recoveryAttempted && finalError.attemptedUrls) {
                this.notifyUser404Error({
                  operation: 'delete document',
                  attemptedUrls: finalError.attemptedUrls
                });
              }
              
              return throwError(() => ({
                ...finalError,
                requestId: requestId
              }));
            })
          );
        }
        
        // Not a 404 error, handle normally
        this.logApiError(requestId, 'DELETE', deleteUrl, error, {
          operation: 'delete_document',
          documentType: 'unknown'
        }, startTime);
        
        return throwError(() => ({
          ...error,
          requestId: requestId
        }));
      })
    );
  }

  /**
   * Retry document verification
   */
  retryVerification(documentId: string): Observable<DocumentVerificationResult> {
    const retryUrl = `${this.baseUrl}/api/Document/${documentId}/retry`;
    const requestId = this.generateRequestId();
    const startTime = Date.now();

    if (environment.demoMode) {
      this.logApiRequest('POST', retryUrl, requestId, { documentId });
      
      const result: DocumentVerificationResult = {
        documentId,
        verificationStatus: 'pending' as const,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'yellow' as const,
        message: 'Retrying verification...',
        requiresUserConfirmation: false
      };

      return of(result).pipe(
        delay(500),
        tap(result => {
          this.logApiResponse(requestId, 200, 'OK', result, startTime);
          this.verificationUpdatesSubject.next(result);
          // Simulate new verification process
          const documentType = 'Document'; // We don't have the type here, so use generic
          this.simulateVerificationProcess(documentId, documentType);
        })
      );
    }

    // Log the API request
    this.logApiRequest('POST', retryUrl, requestId, { documentId });

    return this.http.post<DocumentVerificationResult>(retryUrl, {}).pipe(
      tap(result => {
        this.logApiResponse(requestId, 200, 'OK', result, startTime);
        this.verificationUpdatesSubject.next(result);
      }),
      catchError(error => {
        // Try 404 recovery first
        if (error.status === 404) {
          return this.handle404Error<DocumentVerificationResult>(
            error,
            'POST',
            retryUrl,
            { documentId },
            requestId,
            {
              operation: 'retry_verification',
              documentType: 'unknown'
            }
          ).pipe(
            tap(result => {
              this.verificationUpdatesSubject.next(result);
            }),
            catchError(finalError => {
              this.logApiError(requestId, 'POST', retryUrl, finalError, {
                operation: 'retry_verification',
                documentType: 'unknown'
              }, startTime);
              
              // Show user notification for persistent 404 errors
              if (finalError.recoveryAttempted && finalError.attemptedUrls) {
                this.notifyUser404Error({
                  operation: 'retry verification',
                  attemptedUrls: finalError.attemptedUrls
                });
              }
              
              return throwError(() => ({
                ...finalError,
                requestId: requestId
              }));
            })
          );
        }
        
        // Not a 404 error, handle normally
        this.logApiError(requestId, 'POST', retryUrl, error, {
          operation: 'retry_verification',
          documentType: 'unknown'
        }, startTime);
        
        return throwError(() => ({
          ...error,
          requestId: requestId
        }));
      })
    );
  }

  /**
   * Update upload progress for UI feedback
   */
  private updateUploadProgress(
    documentType: string, 
    progress: number, 
    status: DocumentUploadProgress['status'], 
    message?: string
  ): void {
    const currentProgress = this.uploadProgressSubject.value;
    const existingIndex = currentProgress.findIndex(p => p.documentType === documentType);
    
    const newProgress: DocumentUploadProgress = {
      documentType,
      progress,
      status,
      message
    };

    if (existingIndex >= 0) {
      currentProgress[existingIndex] = newProgress;
    } else {
      currentProgress.push(newProgress);
    }

    this.uploadProgressSubject.next([...currentProgress]);
  }

  /**
   * Get current upload progress for a document type
   */
  getUploadProgress(documentType: string): DocumentUploadProgress | null {
    const progress = this.uploadProgressSubject.value;
    return progress.find(p => p.documentType === documentType) || null;
  }

  /**
   * Clear upload progress for a document type
   */
  clearUploadProgress(documentType: string): void {
    const currentProgress = this.uploadProgressSubject.value;
    const filtered = currentProgress.filter(p => p.documentType !== documentType);
    this.uploadProgressSubject.next(filtered);
  }

  /**
   * Validate file before upload - performs essential security checks only
   * File size validation is handled by Azure Intelligence
   */
  validateFile(file: File): { isValid: boolean; error?: string } {
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'application/pdf'];
    const allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];

    // Check file type
    if (!allowedTypes.includes(file.type.toLowerCase())) {
      return {
        isValid: false,
        error: `File type '${file.type}' is not supported. Allowed types: JPEG, PNG, PDF`
      };
    }

    // Check file extension
    const extension = '.' + file.name.split('.').pop()?.toLowerCase();
    if (!allowedExtensions.includes(extension)) {
      return {
        isValid: false,
        error: `File extension '${extension}' is not allowed. Allowed extensions: ${allowedExtensions.join(', ')}`
      };
    }

    // Check filename
    if (!file.name || file.name.trim().length === 0) {
      return {
        isValid: false,
        error: 'File must have a valid name'
      };
    }

    // Security checks for dangerous file extensions
    const dangerousExtensions = ['.exe', '.bat', '.cmd', '.scr', '.com', '.pif', '.vbs', '.js'];
    if (dangerousExtensions.some(ext => file.name.toLowerCase().endsWith(ext))) {
      return {
        isValid: false,
        error: 'File type is not allowed for security reasons'
      };
    }

    // Check for empty files
    if (file.size === 0) {
      return {
        isValid: false,
        error: 'File is empty. Please select a valid file.'
      };
    }

    return { isValid: true };
  }

  /**
   * Simulate document upload for demo mode
   */
  private simulateDocumentUpload(request: DocumentUploadRequest): Observable<DocumentVerificationResult> {
    const documentId = 'demo-' + Date.now().toString();
    
    // Initialize progress tracking
    this.updateUploadProgress(request.documentType, 0, 'uploading', 'Starting upload...');

    // Simulate upload progress
    return timer(0, 200).pipe(
      map(tick => {
        const progress = Math.min((tick + 1) * 20, 100);
        if (progress < 100) {
          this.updateUploadProgress(request.documentType, progress, 'uploading', 
            `Uploading... ${progress}%`);
          return null;
        } else {
          this.updateUploadProgress(request.documentType, 100, 'processing', 
            'Upload complete, processing document...');
          
          // Track the demo document as uploaded
          this.trackUploadedDocument(documentId);
          
          // Start simulated verification
          this.simulateVerificationProcess(documentId, request.documentType);
          
          return {
            documentId,
            verificationStatus: 'pending' as const,
            isBlurred: false,
            isCorrectType: true,
            statusColor: 'yellow' as const,
            message: 'Processing document...',
            requiresUserConfirmation: false
          };
        }
      }),
      // Take until we get a result (not null)
      switchMap(result => result ? of(result) : of(null)),
      // Filter out null values
      map(result => result as DocumentVerificationResult)
    );
  }

  /**
   * Simulate verification process for demo mode
   */
  private simulateVerificationProcess(documentId: string, documentType: string): void {
    // Simulate processing time (2-4 seconds)
    const processingTime = 2000 + Math.random() * 2000;
    
    setTimeout(() => {
      // Generate random verification result for demo
      const confidenceScore = Math.floor(Math.random() * 100);
      const isBlurred = Math.random() < 0.1; // 10% chance of blur
      const isCorrectType = Math.random() < 0.9; // 90% chance of correct type
      
      let verificationStatus: 'verified' | 'failed';
      let statusColor: 'green' | 'yellow' | 'red';
      let message: string;
      let requiresUserConfirmation = false;

      if (isBlurred) {
        verificationStatus = 'failed';
        statusColor = 'red';
        message = 'Document appears blurred or unclear. Please upload a clearer image.';
      } else if (!isCorrectType) {
        verificationStatus = 'failed';
        statusColor = 'red';
        message = `Document type mismatch. Expected ${documentType}, but detected a different type.`;
      } else if (confidenceScore >= 85) {
        verificationStatus = 'verified';
        statusColor = 'green';
        message = `Document verified successfully with ${confidenceScore}% confidence.`;
      } else if (confidenceScore >= 50) {
        verificationStatus = 'verified';
        statusColor = 'yellow';
        message = `Document verified with moderate confidence (${confidenceScore}%). Please confirm if this is correct.`;
        requiresUserConfirmation = true;
      } else {
        verificationStatus = 'failed';
        statusColor = 'red';
        message = `Document verification failed with low confidence (${confidenceScore}%). Please upload a clearer image.`;
      }

      const result: DocumentVerificationResult = {
        documentId,
        verificationStatus,
        confidenceScore,
        isBlurred,
        isCorrectType,
        statusColor,
        message,
        requiresUserConfirmation
      };

      this.updateUploadProgress(documentType, 100, 'completed', message);
      this.verificationUpdatesSubject.next(result);
    }, processingTime);
  }
}