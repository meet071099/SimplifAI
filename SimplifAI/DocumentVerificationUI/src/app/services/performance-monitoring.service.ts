import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PerformanceStats {
  operationName: string;
  totalCalls: number;
  totalDuration: string;
  averageDuration: string;
  minDuration: string;
  maxDuration: string;
  lastCalled: string;
  errorCount: number;
  successRate: number;
}

export interface CacheStatistics {
  hitCount: number;
  missCount: number;
  setCount: number;
  removeCount: number;
  hitRatio: number;
  lastAccessed: string;
  approximateItemCount: number;
}

export interface SystemHealthInfo {
  timestamp: string;
  status: string;
  version: string;
  environment: string;
  machineName: string;
  processorCount: number;
  workingSet: number;
  upTime: string;
}

@Injectable({
  providedIn: 'root'
})
export class PerformanceMonitoringService {
  private readonly apiUrl = `${environment.apiUrl}/api/monitoring`;
  private performanceMetrics: Map<string, number> = new Map();

  constructor(private http: HttpClient) {}

  /**
   * Gets performance statistics from the backend
   */
  getPerformanceStats(): Observable<{ [key: string]: PerformanceStats }> {
    return this.http.get<{ [key: string]: PerformanceStats }>(`${this.apiUrl}/performance`);
  }

  /**
   * Gets cache statistics from the backend
   */
  getCacheStats(): Observable<CacheStatistics> {
    return this.http.get<CacheStatistics>(`${this.apiUrl}/cache`);
  }

  /**
   * Gets system health information
   */
  getSystemHealth(): Observable<SystemHealthInfo> {
    return this.http.get<SystemHealthInfo>(`${this.apiUrl}/health`);
  }

  /**
   * Clears the cache (admin function)
   */
  clearCache(): Observable<any> {
    return this.http.post(`${this.apiUrl}/cache/clear`, {});
  }

  /**
   * Gets slow operations
   */
  getSlowOperations(limit: number = 10): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/performance/slow?limit=${limit}`);
  }

  /**
   * Starts timing a client-side operation
   */
  startTimer(operationName: string): () => void {
    const startTime = performance.now();
    
    return () => {
      const endTime = performance.now();
      const duration = endTime - startTime;
      this.recordClientMetric(operationName, duration);
    };
  }

  /**
   * Records a client-side performance metric
   */
  private recordClientMetric(operationName: string, duration: number): void {
    // Store in local metrics for potential reporting
    this.performanceMetrics.set(`${operationName}_${Date.now()}`, duration);
    
    // Log slow operations
    if (duration > 1000) { // 1 second threshold
      console.warn(`Slow client operation detected: ${operationName} took ${duration.toFixed(2)}ms`);
    }
    
    // Keep only recent metrics (last 100)
    if (this.performanceMetrics.size > 100) {
      const firstKey = this.performanceMetrics.keys().next().value;
      this.performanceMetrics.delete(firstKey);
    }
  }

  /**
   * Gets client-side performance metrics
   */
  getClientMetrics(): { operation: string; duration: number; timestamp: number }[] {
    const metrics: { operation: string; duration: number; timestamp: number }[] = [];
    
    this.performanceMetrics.forEach((duration, key) => {
      const [operation, timestamp] = key.split('_');
      metrics.push({
        operation,
        duration,
        timestamp: parseInt(timestamp)
      });
    });
    
    return metrics.sort((a, b) => b.timestamp - a.timestamp);
  }

  /**
   * Measures the performance of an async operation
   */
  async measureAsync<T>(operationName: string, operation: () => Promise<T>): Promise<T> {
    const stopTimer = this.startTimer(operationName);
    try {
      const result = await operation();
      return result;
    } finally {
      stopTimer();
    }
  }

  /**
   * Measures the performance of a synchronous operation
   */
  measure<T>(operationName: string, operation: () => T): T {
    const stopTimer = this.startTimer(operationName);
    try {
      return operation();
    } finally {
      stopTimer();
    }
  }
}