import { Component, OnInit, OnDestroy } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { PerformanceMonitoringService, PerformanceStats, CacheStatistics, SystemHealthInfo } from '../../../services/performance-monitoring.service';

@Component({
  selector: 'app-performance-monitor',
  templateUrl: './performance-monitor.component.html',
  styleUrls: ['./performance-monitor.component.css']
})
export class PerformanceMonitorComponent implements OnInit, OnDestroy {
  performanceStats: { [key: string]: PerformanceStats } = {};
  cacheStats: CacheStatistics | null = null;
  systemHealth: SystemHealthInfo | null = null;
  clientMetrics: any[] = [];
  isVisible = false;
  refreshSubscription?: Subscription;
  
  constructor(private performanceService: PerformanceMonitoringService) {}

  ngOnInit(): void {
    this.loadData();
    
    // Auto-refresh every 30 seconds when visible
    this.refreshSubscription = interval(30000).subscribe(() => {
      if (this.isVisible) {
        this.loadData();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.refreshSubscription) {
      this.refreshSubscription.unsubscribe();
    }
  }

  toggleVisibility(): void {
    this.isVisible = !this.isVisible;
    if (this.isVisible) {
      this.loadData();
    }
  }

  private loadData(): void {
    // Load server-side performance stats
    this.performanceService.getPerformanceStats().subscribe({
      next: (stats) => this.performanceStats = stats,
      error: (error) => console.error('Error loading performance stats:', error)
    });

    // Load cache statistics
    this.performanceService.getCacheStats().subscribe({
      next: (stats) => this.cacheStats = stats,
      error: (error) => console.error('Error loading cache stats:', error)
    });

    // Load system health
    this.performanceService.getSystemHealth().subscribe({
      next: (health) => this.systemHealth = health,
      error: (error) => console.error('Error loading system health:', error)
    });

    // Load client-side metrics
    this.clientMetrics = this.performanceService.getClientMetrics();
  }

  clearCache(): void {
    if (confirm('Are you sure you want to clear the cache? This may temporarily impact performance.')) {
      this.performanceService.clearCache().subscribe({
        next: () => {
          alert('Cache cleared successfully');
          this.loadData();
        },
        error: (error) => {
          console.error('Error clearing cache:', error);
          alert('Error clearing cache');
        }
      });
    }
  }

  getPerformanceStatsArray(): PerformanceStats[] {
    return Object.values(this.performanceStats);
  }

  formatDuration(duration: string): string {
    // Convert duration string to milliseconds for display
    const match = duration.match(/(\d+):(\d+):(\d+)\.(\d+)/);
    if (match) {
      const hours = parseInt(match[1]);
      const minutes = parseInt(match[2]);
      const seconds = parseInt(match[3]);
      const milliseconds = parseInt(match[4].substring(0, 3));
      
      const totalMs = hours * 3600000 + minutes * 60000 + seconds * 1000 + milliseconds;
      return `${totalMs}ms`;
    }
    return duration;
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  getStatusColor(status: string): string {
    switch (status.toLowerCase()) {
      case 'healthy': return 'green';
      case 'warning': return 'orange';
      case 'error': return 'red';
      default: return 'gray';
    }
  }
}