import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { RecruiterService } from '../../services/recruiter.service';
import { NotificationService } from '../../services/notification.service';

export interface FormSummary {
  id: string;
  candidateName: string;
  candidateEmail: string;
  status: string;
  submittedAt?: Date;
  createdAt: Date;
  documentCount: number;
  hasLowConfidenceDocuments: boolean;
  recruiterEmail: string;
}

export interface DashboardStats {
  totalForms: number;
  submittedForms: number;
  approvedForms: number;
  rejectedForms: number;
  pendingReview: number;
  formsWithLowConfidenceDocuments: number;
}

@Component({
  selector: 'app-recruiter-dashboard',
  templateUrl: './recruiter-dashboard.component.html',
  styleUrls: ['./recruiter-dashboard.component.css']
})
export class RecruiterDashboardComponent implements OnInit {
  forms: FormSummary[] = [];
  stats: DashboardStats = {
    totalForms: 0,
    submittedForms: 0,
    approvedForms: 0,
    rejectedForms: 0,
    pendingReview: 0,
    formsWithLowConfidenceDocuments: 0
  };
  
  // Filters and pagination
  searchTerm = '';
  statusFilter = '';
  currentPage = 1;
  pageSize = 10;
  totalPages = 0;
  totalCount = 0;
  
  // Loading states
  isLoading = false;
  isLoadingStats = false;
  
  // Filter options
  statusOptions = [
    { value: '', label: 'All Statuses' },
    { value: 'Submitted', label: 'Submitted' },
    { value: 'Under Review', label: 'Under Review' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Rejected', label: 'Rejected' }
  ];

  constructor(
    private recruiterService: RecruiterService,
    private notificationService: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadDashboardData();
  }

  async loadDashboardData(): Promise<void> {
    await Promise.all([
      this.loadForms(),
      this.loadStats()
    ]);
  }

  async loadForms(): Promise<void> {
    this.isLoading = true;
    try {
      const result = await this.recruiterService.getSubmittedForms(
        undefined, // recruiterEmail - could be filtered later
        this.statusFilter || undefined,
        this.searchTerm || undefined,
        this.currentPage,
        this.pageSize
      );
      
      this.forms = result.forms;
      this.totalCount = result.totalCount;
      this.totalPages = result.totalPages;
    } catch (error) {
      console.error('Error loading forms:', error);
      this.notificationService.showError('Error', 'Failed to load forms');
    } finally {
      this.isLoading = false;
    }
  }

  async loadStats(): Promise<void> {
    this.isLoadingStats = true;
    try {
      this.stats = await this.recruiterService.getDashboardStats();
    } catch (error) {
      console.error('Error loading stats:', error);
      this.notificationService.showError('Error', 'Failed to load dashboard statistics');
    } finally {
      this.isLoadingStats = false;
    }
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.loadForms();
  }

  onStatusFilterChange(): void {
    this.currentPage = 1;
    this.loadForms();
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.loadForms();
  }

  viewFormDetails(formId: string): void {
    this.router.navigate(['/recruiter/forms', formId]);
  }

  getStatusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'submitted':
        return 'status-submitted';
      case 'under review':
        return 'status-under-review';
      case 'approved':
        return 'status-approved';
      case 'rejected':
        return 'status-rejected';
      default:
        return 'status-default';
    }
  }

  getStatusIcon(status: string): string {
    switch (status.toLowerCase()) {
      case 'submitted':
        return '📋';
      case 'under review':
        return '👀';
      case 'approved':
        return '✅';
      case 'rejected':
        return '❌';
      default:
        return '📄';
    }
  }

  formatDate(date: Date | string | undefined): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString() + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  getPaginationPages(): number[] {
    const pages: number[] = [];
    const start = Math.max(1, this.currentPage - 2);
    const end = Math.min(this.totalPages, this.currentPage + 2);
    
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    
    return pages;
  }

  refresh(): void {
    this.loadDashboardData();
  }
}