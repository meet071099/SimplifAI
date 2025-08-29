import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { RecruiterService } from '../../services/recruiter.service';
import { NotificationService } from '../../services/notification.service';

export interface PersonalInfo {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  dateOfBirth?: Date;
  createdAt: Date;
}

export interface DocumentReview {
  id: string;
  documentType: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedAt: Date;
  verificationStatus: string;
  confidenceScore?: number;
  isBlurred: boolean;
  isCorrectType: boolean;
  statusColor: string;
  verificationDetails?: string;
  filePath: string;
}

export interface FormReview {
  id: string;
  status: string;
  createdAt: Date;
  submittedAt?: Date;
  recruiterEmail: string;
  personalInfo?: PersonalInfo;
  documents: DocumentReview[];
}

@Component({
  selector: 'app-form-review',
  templateUrl: './form-review.component.html',
  styleUrls: ['./form-review.component.css']
})
export class FormReviewComponent implements OnInit {
  formId: string = '';
  form: FormReview | null = null;
  isLoading = false;
  isUpdatingStatus = false;
  
  // Status update
  showStatusModal = false;
  selectedStatus = '';
  reviewNotes = '';
  
  statusOptions = [
    { value: 'Under Review', label: 'Under Review', icon: '👀' },
    { value: 'Approved', label: 'Approved', icon: '✅' },
    { value: 'Rejected', label: 'Rejected', icon: '❌' }
  ];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private recruiterService: RecruiterService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.formId = params['id'];
      if (this.formId) {
        this.loadFormDetails();
      }
    });
  }

  async loadFormDetails(): Promise<void> {
    this.isLoading = true;
    try {
      this.form = await this.recruiterService.getFormForReview(this.formId);
    } catch (error) {
      console.error('Error loading form details:', error);
      this.notificationService.showError('Error', 'Failed to load form details');
      this.router.navigate(['/recruiter/dashboard']);
    } finally {
      this.isLoading = false;
    }
  }

  goBack(): void {
    this.router.navigate(['/recruiter/dashboard']);
  }

  openStatusModal(): void {
    this.selectedStatus = this.form?.status || '';
    this.reviewNotes = '';
    this.showStatusModal = true;
  }

  closeStatusModal(): void {
    this.showStatusModal = false;
    this.selectedStatus = '';
    this.reviewNotes = '';
  }

  async updateFormStatus(): Promise<void> {
    if (!this.selectedStatus || !this.form) {
      return;
    }

    this.isUpdatingStatus = true;
    try {
      await this.recruiterService.updateFormStatus(this.form.id, this.selectedStatus, this.reviewNotes);
      this.form.status = this.selectedStatus;
      this.notificationService.showSuccess('Success', `Form status updated to ${this.selectedStatus}`);
      this.closeStatusModal();
    } catch (error) {
      console.error('Error updating form status:', error);
      this.notificationService.showError('Error', 'Failed to update form status');
    } finally {
      this.isUpdatingStatus = false;
    }
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

  getConfidenceClass(score?: number): string {
    if (!score) return 'confidence-unknown';
    if (score >= 85) return 'confidence-high';
    if (score >= 50) return 'confidence-medium';
    return 'confidence-low';
  }

  getConfidenceIcon(score?: number): string {
    if (!score) return '❓';
    if (score >= 85) return '✅';
    if (score >= 50) return '⚠️';
    return '❌';
  }

  formatDate(date: Date | string | undefined): string {
    if (!date) return 'N/A';
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleDateString() + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  viewDocument(document: DocumentReview): void {
    // This would typically open a document viewer modal or navigate to a document view
    // For now, we'll show a notification
    this.notificationService.showInfo('Document Viewer', `Document viewer for ${document.fileName} would open here`);
  }

  downloadDocument(document: DocumentReview): void {
    // This would typically trigger a download
    this.notificationService.showInfo('Download', `Download for ${document.fileName} would start here`);
  }

  hasLowConfidenceDocuments(): boolean {
    return this.form?.documents.some(doc => (doc.confidenceScore || 0) < 50) || false;
  }

  getDocumentStatusMessage(document: DocumentReview): string {
    if (document.isBlurred) {
      return 'Document appears blurred';
    }
    if (!document.isCorrectType) {
      return `Document type mismatch. Expected ${document.documentType}`;
    }
    if ((document.confidenceScore || 0) >= 85) {
      return 'Document verified successfully';
    }
    if ((document.confidenceScore || 0) >= 50) {
      return 'Document verification completed with medium confidence';
    }
    return 'Document verification completed with low confidence';
  }
}