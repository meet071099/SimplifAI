import { Component, OnInit, OnDestroy, Input } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { FormService } from '../../services/form.service';
import { FormData, PersonalInfo, DocumentInfo } from '../../models/form.models';

@Component({
  selector: 'app-review-step',
  templateUrl: './review-step.component.html',
  styleUrls: ['./review-step.component.css']
})
export class ReviewStepComponent implements OnInit, OnDestroy {
  @Input() formId?: string;
  formData: FormData | null = null;
  isSubmitting = false;
  submitError: string | null = null;
  
  private destroy$ = new Subject<void>();

  constructor(
    private formService: FormService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Subscribe to form data changes
    this.formService.formData$.pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.formData = data;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get personalInfo(): PersonalInfo | null {
    return this.formData?.personalInfo || null;
  }

  get documents(): DocumentInfo[] {
    return this.formData?.documents || [];
  }

  get uploadedDocuments(): DocumentInfo[] {
    return this.documents.filter(doc => doc.fileName);
  }

  get verifiedDocuments(): DocumentInfo[] {
    return this.documents.filter(doc => 
      doc.fileName && doc.verificationStatus === 'verified'
    );
  }

  get failedDocuments(): DocumentInfo[] {
    return this.documents.filter(doc => 
      doc.fileName && doc.verificationStatus === 'failed'
    );
  }

  get pendingDocuments(): DocumentInfo[] {
    return this.documents.filter(doc => 
      doc.fileName && doc.verificationStatus === 'pending'
    );
  }

  isFormValid(): boolean {
    const validation = this.formService.validateReviewStep();
    return validation.isValid;
  }

  getValidationErrors(): string[] {
    const validation = this.formService.validateReviewStep();
    return validation.errors;
  }

  formatDate(date: Date | null): string {
    if (!date) return 'Not provided';
    return new Date(date).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  getDocumentStatusClass(document: DocumentInfo): string {
    if (document.verificationStatus === 'pending') return 'status-pending';
    if (document.statusColor === 'green') return 'status-success';
    if (document.statusColor === 'yellow') return 'status-warning';
    if (document.statusColor === 'red') return 'status-error';
    return '';
  }

  getDocumentStatusIcon(document: DocumentInfo): string {
    if (document.verificationStatus === 'pending') return '⏳';
    if (document.statusColor === 'green') return '✓';
    if (document.statusColor === 'yellow') return '⚠';
    if (document.statusColor === 'red') return '✗';
    return '';
  }

  editPersonalInfo(): void {
    this.router.navigate(['/form', 'current', 'personal-info']);
  }

  editDocuments(): void {
    this.router.navigate(['/form', 'current', 'document-upload']);
  }

  async submitForm(): Promise<void> {
    if (!this.isFormValid()) {
      this.submitError = 'Please correct all errors before submitting the form.';
      return;
    }

    this.isSubmitting = true;
    this.submitError = null;

    try {
      await this.formService.submitForm().toPromise();
      
      // Show success message and redirect or show confirmation
      alert('Form submitted successfully! You will receive a confirmation email shortly.');
      
      // In a real application, you might redirect to a success page
      // this.router.navigate(['/success']);
      
    } catch (error) {
      console.error('Form submission error:', error);
      this.submitError = 'There was an error submitting your form. Please try again.';
    } finally {
      this.isSubmitting = false;
    }
  }

  getPersonalInfoCompleteness(): number {
    if (!this.personalInfo) return 0;
    
    const fields = [
      this.personalInfo.firstName,
      this.personalInfo.lastName,
      this.personalInfo.email,
      this.personalInfo.phone,
      this.personalInfo.address,
      this.personalInfo.dateOfBirth
    ];
    
    const completedFields = fields.filter(field => 
      field !== null && field !== undefined && field !== ''
    ).length;
    
    return Math.round((completedFields / fields.length) * 100);
  }

  getDocumentCompleteness(): number {
    const totalDocuments = this.documents.length;
    const uploadedDocuments = this.uploadedDocuments.length;
    
    if (totalDocuments === 0) return 0;
    return Math.round((uploadedDocuments / totalDocuments) * 100);
  }

  getOverallCompleteness(): number {
    const personalInfoWeight = 0.4;
    const documentsWeight = 0.6;
    
    const personalInfoScore = this.getPersonalInfoCompleteness() * personalInfoWeight;
    const documentsScore = this.getDocumentCompleteness() * documentsWeight;
    
    return Math.round(personalInfoScore + documentsScore);
  }
}