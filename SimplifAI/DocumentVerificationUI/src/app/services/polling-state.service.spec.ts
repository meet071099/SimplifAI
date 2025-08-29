import { TestBed } from '@angular/core/testing';
import { PollingStateService, PollingSession, PollingConfig } from './polling-state.service';

describe('PollingStateService', () => {
  let service: PollingStateService;
  let mockSessionStorage: { [key: string]: string };

  const mockPollingConfig: PollingConfig = {
    initialDelay: 1000,
    retryIntervals: [2000, 5000, 10000],
    maxRetries: 5,
    backoffMultiplier: 1.5,
    timeoutMs: 60000,
    jitterMaxMs: 1000
  };

  beforeEach(() => {
    // Mock sessionStorage
    mockSessionStorage = {};
    spyOn(sessionStorage, 'getItem').and.callFake((key: string) => mockSessionStorage[key] || null);
    spyOn(sessionStorage, 'setItem').and.callFake((key: string, value: string) => {
      mockSessionStorage[key] = value;
    });

    TestBed.configureTestingModule({});
    service = TestBed.inject(PollingStateService);
  });

  afterEach(() => {
    service.cleanupAllPollingResources();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('startPollingSession', () => {
    it('should start a new polling session', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      const sessionId = service.startPollingSession(documentId, documentType, mockPollingConfig);
      
      expect(sessionId).toBeTruthy();
      expect(service.isPollingActive(documentId)).toBe(true);
      
      const session = service.getPollingSession(documentId);
      expect(session).toBeTruthy();
      expect(session!.documentId).toBe(documentId);
      expect(session!.documentType).toBe(documentType);
      expect(session!.status).toBe('active');
      expect(session!.sessionId).toBe(sessionId);
    });

    it('should prevent multiple concurrent polling for the same document', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      const sessionId1 = service.startPollingSession(documentId, documentType, mockPollingConfig);
      const sessionId2 = service.startPollingSession(documentId, documentType, mockPollingConfig);
      
      expect(sessionId1).toBe(sessionId2);
      expect(service.getActivePollingDocuments()).toEqual([documentId]);
    });

    it('should emit polling state updates', (done) => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      service.pollingUpdates$.subscribe(update => {
        if (update) {
          expect(update.documentId).toBe(documentId);
          expect(update.status).toBe('active');
          expect(update.details?.requestCount).toBe(0);
          done();
        }
      });
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
    });
  });

  describe('stopPollingSession', () => {
    it('should stop polling session with completed status', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      const sessionId = service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.stopPollingSession(documentId, 'completed');
      
      const session = service.getPollingSession(documentId);
      expect(session!.status).toBe('completed');
      expect(session!.endTime).toBeTruthy();
      expect(service.isPollingActive(documentId)).toBe(false);
    });

    it('should stop polling session with error', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      const error = { status: 404, message: 'Document not found' };
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.stopPollingSession(documentId, 'failed', error);
      
      const session = service.getPollingSession(documentId);
      expect(session!.status).toBe('failed');
      expect(session!.lastError).toEqual({
        ...error,
        timestamp: jasmine.any(Number)
      });
    });

    it('should clear active timer when stopping session', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      const mockTimerId = setTimeout(() => {}, 1000);
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.setPollingTimer(documentId, mockTimerId);
      
      spyOn(window, 'clearTimeout');
      service.stopPollingSession(documentId, 'completed');
      
      expect(clearTimeout).toHaveBeenCalledWith(mockTimerId);
    });

    it('should emit polling update when stopping session', (done) => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      let updateCount = 0;
      
      service.pollingUpdates$.subscribe(update => {
        if (update) {
          updateCount++;
          if (updateCount === 2) { // Second update is the stop event
            expect(update.status).toBe('completed');
            done();
          }
        }
      });
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.stopPollingSession(documentId, 'completed');
    });
  });

  describe('updatePollingSession', () => {
    it('should update polling session properties', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      
      const updates = {
        totalRequests: 3,
        successfulRequests: 2,
        failedRequests: 1,
        currentRetryCount: 2
      };
      
      service.updatePollingSession(documentId, updates);
      
      const session = service.getPollingSession(documentId);
      expect(session!.totalRequests).toBe(3);
      expect(session!.successfulRequests).toBe(2);
      expect(session!.failedRequests).toBe(1);
      expect(session!.currentRetryCount).toBe(2);
    });

    it('should handle updates for non-existent session', () => {
      spyOn(console, 'warn');
      
      service.updatePollingSession('non-existent', { totalRequests: 1 });
      
      expect(console.warn).toHaveBeenCalledWith(
        'PollingStateService: Cannot update polling session - no session found for document non-existent'
      );
    });
  });

  describe('queryPollingStatus', () => {
    it('should return polling status for active session', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.updatePollingSession(documentId, { totalRequests: 5 });
      
      const status = service.queryPollingStatus(documentId);
      
      expect(status.isActive).toBe(true);
      expect(status.session).toBeTruthy();
      expect(status.requestCount).toBe(5);
      expect(status.duration).toBeGreaterThan(0);
    });

    it('should return inactive status for non-existent session', () => {
      const status = service.queryPollingStatus('non-existent');
      
      expect(status.isActive).toBe(false);
      expect(status.session).toBeUndefined();
    });
  });

  describe('timer management', () => {
    it('should set and clear polling timers', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      const mockTimerId = setTimeout(() => {}, 1000);
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.setPollingTimer(documentId, mockTimerId);
      
      const session = service.getPollingSession(documentId);
      expect(session!.timerId).toBe(mockTimerId);
      
      spyOn(window, 'clearTimeout');
      service.clearPollingTimer(documentId);
      
      expect(clearTimeout).toHaveBeenCalledWith(mockTimerId);
      expect(session!.timerId).toBeUndefined();
    });

    it('should clear existing timer when setting new one', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      const oldTimerId = setTimeout(() => {}, 1000);
      const newTimerId = setTimeout(() => {}, 2000);
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.setPollingTimer(documentId, oldTimerId);
      
      spyOn(window, 'clearTimeout');
      service.setPollingTimer(documentId, newTimerId);
      
      expect(clearTimeout).toHaveBeenCalledWith(oldTimerId);
      
      const session = service.getPollingSession(documentId);
      expect(session!.timerId).toBe(newTimerId);
    });
  });

  describe('cleanup operations', () => {
    it('should cancel all active polling sessions', () => {
      const doc1 = 'test-doc-1';
      const doc2 = 'test-doc-2';
      const documentType = 'passport';
      
      service.startPollingSession(doc1, documentType, mockPollingConfig);
      service.startPollingSession(doc2, documentType, mockPollingConfig);
      
      expect(service.getActivePollingDocuments()).toEqual([doc1, doc2]);
      
      service.cancelAllPolling();
      
      expect(service.getActivePollingDocuments()).toEqual([]);
      expect(service.getPollingSession(doc1)!.status).toBe('cancelled');
      expect(service.getPollingSession(doc2)!.status).toBe('cancelled');
    });

    it('should cleanup all polling resources', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      const mockTimerId = setTimeout(() => {}, 1000);
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.setPollingTimer(documentId, mockTimerId);
      
      spyOn(window, 'clearTimeout');
      service.cleanupAllPollingResources();
      
      expect(clearTimeout).toHaveBeenCalledWith(mockTimerId);
      expect(service.getAllPollingSessions()).toEqual([]);
    });

    it('should cleanup resources for specific document', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      const mockTimerId = setTimeout(() => {}, 1000);
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.setPollingTimer(documentId, mockTimerId);
      
      spyOn(window, 'clearTimeout');
      service.cleanupPollingResources(documentId);
      
      expect(clearTimeout).toHaveBeenCalledWith(mockTimerId);
      expect(service.getPollingSession(documentId)!.status).toBe('cancelled');
    });
  });

  describe('state persistence', () => {
    it('should persist polling state to session storage', () => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      
      expect(sessionStorage.setItem).toHaveBeenCalledWith(
        'pollingState',
        jasmine.any(String)
      );
      
      const storedData = JSON.parse(mockSessionStorage['pollingState']);
      expect(storedData.length).toBe(1);
      expect(storedData[0].documentId).toBe(documentId);
      expect(storedData[0].session.documentType).toBe(documentType);
    });

    it('should restore polling state from session storage', () => {
      const testData = [{
        documentId: 'test-doc-1',
        session: {
          documentId: 'test-doc-1',
          documentType: 'passport',
          sessionId: 'test-session-1',
          status: 'active',
          startTime: Date.now() - 5000,
          totalRequests: 2,
          successfulRequests: 1,
          failedRequests: 1,
          currentRetryCount: 1,
          maxRetries: 5,
          lastRequestTime: Date.now() - 1000
        }
      }];
      
      mockSessionStorage['pollingState'] = JSON.stringify(testData);
      
      // Create new service instance to trigger restoration
      const newService = new PollingStateService();
      
      const sessions = newService.getAllPollingSessions();
      expect(sessions.length).toBe(1);
      expect(sessions[0].documentId).toBe('test-doc-1');
      expect(sessions[0].status).toBe('cancelled'); // Active sessions are marked as cancelled on restore
    });
  });

  describe('polling statistics', () => {
    it('should calculate polling statistics correctly', () => {
      const doc1 = 'test-doc-1';
      const doc2 = 'test-doc-2';
      const documentType = 'passport';
      
      // Create first session
      service.startPollingSession(doc1, documentType, mockPollingConfig);
      service.updatePollingSession(doc1, {
        totalRequests: 5,
        successfulRequests: 4,
        failedRequests: 1
      });
      service.stopPollingSession(doc1, 'completed');
      
      // Create second session
      service.startPollingSession(doc2, documentType, mockPollingConfig);
      service.updatePollingSession(doc2, {
        totalRequests: 3,
        successfulRequests: 2,
        failedRequests: 1
      });
      service.stopPollingSession(doc2, 'failed');
      
      const stats = service.getPollingStatistics();
      
      expect(stats.totalSessions).toBe(2);
      expect(stats.completedSessions).toBe(1);
      expect(stats.failedSessions).toBe(1);
      expect(stats.totalRequests).toBe(8);
      expect(stats.successRate).toBe(75); // 6 successful out of 8 total
    });

    it('should handle empty statistics', () => {
      const stats = service.getPollingStatistics();
      
      expect(stats.totalSessions).toBe(0);
      expect(stats.activeSessions).toBe(0);
      expect(stats.averageSessionDuration).toBe(0);
      expect(stats.totalRequests).toBe(0);
      expect(stats.successRate).toBe(0);
    });
  });

  describe('observable streams', () => {
    it('should emit state changes through pollingState$ observable', (done) => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      
      service.pollingState$.subscribe(stateMap => {
        if (stateMap.size > 0) {
          expect(stateMap.has(documentId)).toBe(true);
          const session = stateMap.get(documentId);
          expect(session!.status).toBe('active');
          done();
        }
      });
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
    });

    it('should emit updates through pollingUpdates$ observable', (done) => {
      const documentId = 'test-doc-1';
      const documentType = 'passport';
      let updateCount = 0;
      
      service.pollingUpdates$.subscribe(update => {
        if (update) {
          updateCount++;
          expect(update.documentId).toBe(documentId);
          
          if (updateCount === 1) {
            expect(update.status).toBe('active');
          } else if (updateCount === 2) {
            expect(update.status).toBe('completed');
            done();
          }
        }
      });
      
      service.startPollingSession(documentId, documentType, mockPollingConfig);
      service.stopPollingSession(documentId, 'completed');
    });
  });
});