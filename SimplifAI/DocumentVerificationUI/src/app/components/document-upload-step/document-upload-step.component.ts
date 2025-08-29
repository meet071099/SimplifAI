import { Component, OnInit, OnDestroy, Input } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { FormService } from '../../services/form.service';
import { DocumentService, DocumentUploadProgress, ErrorResponse } from '../../services/document.service';
import { NotificationService } from '../../services/notification.service';
import { DocumentInfo } from '../../models/form.models';
import { FileUploadEvent } from '../shared/file-upload/file-upload.component';
import { DocumentAction } from '../shared/document-verification/document-verification.component';
import { AlertType } from '../shared/alert/alert.component';

@Component({
  selector: 'app-document-upload-step',
  templateUrl: './document-upload-step.component.html',
  styleUrls: ['./document-upload-step.component.css']
})
export class DocumentUploadStepComponent implements OnInit, OnDestroy {
  @Input() formId?: string;
  documents: DocumentInfo[] = [];
  uploadProgress: { [key: string]: DocumentUploadProgress } = {};
  alertMessage: string = '';
  alertType: AlertType = 'info';
  
  private destroy$ = new Subject<void>();
  private currentFormId: string = '';

  readonly allowedFileTypes = ['image/jpeg', 'image/jpg', 'image/png', 'application/pdf'];
  readonly allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];

  constructor(
    private formService: FormService,
    private documentService: DocumentService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    // Use the formId input if provided
    if (this.formId) {
      this.currentFormId = this.formId;
    }
    
    // Subscribe to form data changes
    this.formService.formData$.pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.documents = [...data.documents];
      if (!this.currentFormId) {
        this.currentFormId = data.id || '';
      }
    });

    // Subscribe to upload progress updates
    this.documentService.uploadProgress$
      .pipe(takeUntil(this.destroy$))
      .subscribe(progressList => {
        progressList.forEach(progress => {
          this.uploadProgress[progress.documentType] = progress;
        });
      });

    // Subscribe to verification updates
    this.documentService.verificationUpdates$
      .pipe(takeUntil(this.destroy$))
      .subscribe(result => {
        this.handleVerificationResult(result);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onFileUploaded(event: FileUploadEvent): void {
    this.handleFileSelection(event.file, event.documentType);
  }

  onDocumentAction(action: DocumentAction): void {
    switch (action.action) {
      case 'confirm':
        this.confirmDocument(action.documentType);
        break;
      case 'replace':
        this.replaceDocument(action.documentType);
        break;
      case 'remove':
        this.removeDocument(action.documentType);
        break;
    }
  }

  private confirmDocument(documentType: string): void {
    const document = this.getDocumentByType(documentType);
    if (document && document.id) {
      // Show confirmation dialog for low confidence documents
      if (document.confidenceScore !== undefined && document.confidenceScore < 85) {
        this.notificationService.showConfidenceConfirmation(
          documentType,
          document.confidenceScore,
          () => {
            // User confirmed - proceed with document
            this.documentService.confirmDocument(document.id!).subscribe({
              next: (result) => {
                this.updateDocumentFromResult(documentType, result);
                this.notificationService.showSuccess(
                  'Document Confirmed',
                  `${documentType} has been confirmed and accepted.`
                );
              },
              error: (error) => {
                const categorizedError = this.documentService.getCategorizedError(error) || 
                                        this.createFallbackError(error);
                this.notificationService.showError(
                  'Confirmation Failed',
                  `${categorizedError.message} ${categorizedError.suggestedAction || ''}`
                );
              }
            });
          },
          () => {
            // User wants to replace - trigger file upload
            this.replaceDocument(documentType);
          }
        );
      } else {
        // High confidence document - confirm directly
        this.documentService.confirmDocument(document.id).subscribe({
          next: (result) => {
            this.updateDocumentFromResult(documentType, result);
            this.notificationService.showSuccess(
              'Document Confirmed',
              `${documentType} has been confirmed and accepted.`
            );
          },
          error: (error) => {
            const categorizedError = this.documentService.getCategorizedError(error) || 
                                    this.createFallbackError(error);
            this.notificationService.showError(
              'Confirmation Failed',
              `${categorizedError.message} ${categorizedError.suggestedAction || ''}`
            );
          }
        });
      }
    }
  }

  clearAlert(): void {
    this.alertMessage = '';
  }

  private showAlert(message: string, type: AlertType): void {
    this.alertMessage = message;
    this.alertType = type;
    // Auto-clear success messages after 3 seconds
    if (type === 'success') {
      setTimeout(() => this.clearAlert(), 3000);
    }
  }

  private handleFileSelection(file: File, documentType: string): void {
    // Validate file using document service
    const validation = this.documentService.validateFile(file);
    if (!validation.isValid) {
      this.notificationService.showError('Invalid File', validation.error!);
      return;
    }

    // Clear any existing progress for this document type
    this.documentService.clearUploadProgress(documentType);

    // Upload document with real-time progress
    this.documentService.uploadDocument({
      file,
      documentType,
      formId: this.currentFormId
    }).subscribe({
      next: (result) => {
        if (result) {
          // Update form service with initial upload result
          this.formService.updateDocument(documentType, {
            id: result.documentId,
            fileName: file.name,
            file: file,
            verificationStatus: 'pending',
            message: 'Processing document...'
          });
        }
      },
      error: (error) => {
        this.handleUploadError(error, documentType);
      }
    });
  }

  /**
   * Handle verification results from the document service
   */
  private handleVerificationResult(result: any): void {
    const documentType = this.findDocumentTypeById(result.documentId);
    if (!documentType) return;

    this.updateDocumentFromResult(documentType, result);

    // Show appropriate feedback based on verification result
    if (result.isBlurred) {
      this.notificationService.showBlurDetectionFeedback(
        documentType,
        () => this.replaceDocument(documentType)
      );
    } else if (!result.isCorrectType) {
      this.notificationService.showTypeMismatchFeedback(
        documentType,
        'different document type',
        () => this.replaceDocument(documentType),
        result.requiresUserConfirmation ? () => this.confirmDocument(documentType) : undefined
      );
    } else if (result.verificationStatus == 'Failed') {
      // Handle authenticity verification failures with detailed reasons from the new API response
      const reason = result.message || 'Personal information does not match form data';
      
      this.notificationService.showError(
        'Document Verification Failed',
        `${documentType} verification failed: ${reason}. Please upload a correct document or ensure your personal information matches exactly.`
      );
    } else if (result.requiresUserConfirmation) {
      // Document is correct type and not blurred, but has low/medium confidence
      this.notificationService.showConfidenceConfirmation(
        documentType,
        result.confidenceScore,
        () => this.confirmDocument(documentType),
        () => this.replaceDocument(documentType)
      );
    } else if (result.verificationStatus === 'verified' && result.statusColor === 'green') {
      // High confidence verification - show success
      const successMessage = result.authenticityVerified 
        ? `${documentType} has been successfully verified with AI authenticity check (${result.confidenceScore}% confidence).`
        : `${documentType} has been successfully verified with ${result.confidenceScore}% confidence.`;
      
      this.notificationService.showSuccess(
        'Document Verified',
        successMessage
      );
    }
  }

  /**
   * Update document in form service from verification result
   */
  private updateDocumentFromResult(documentType: string, result: any): void {
    // Extract authenticity reason from the result if available
    let authenticityReason: string | undefined;
    let authenticityRawResponse: string | undefined;
    
    if (result.authenticityResult) {
      const fullResponse = result.authenticityResult;
      
      // Store the complete raw response from the AI agent
      authenticityRawResponse = fullResponse;
      
      // Check if it's the structured format with colon
      if (fullResponse.includes(':')) {
        const colonIndex = fullResponse.indexOf(':');
        const afterColon = fullResponse.substring(colonIndex + 1).trim();
        
        // Keep the complete raw message after the colon as the reason
        authenticityReason = afterColon;
      } else {
        // Handle cases where the response might be just the reason without prefix
        authenticityReason = fullResponse;
      }
    }

    this.formService.updateDocument(documentType, {
      id: result.documentId,
      verificationStatus: result.verificationStatus,
      confidenceScore: result.confidenceScore,
      statusColor: result.statusColor,
      isBlurred: result.isBlurred,
      isCorrectType: result.isCorrectType,
      message: result.message,
      // Add authenticity fields
      authenticityResult: result.authenticityResult,
      authenticityVerified: result.authenticityVerified,
      authenticityReason: authenticityReason, // Complete raw reason from AI
      authenticityRawResponse: authenticityRawResponse, // Store complete raw response
      aiFoundryUsed: result.aiFoundryUsed
    });
  }

  /**
   * Find document type by document ID
   */
  private findDocumentTypeById(documentId: string): string | null {
    const document = this.documents.find(d => d.id === documentId);
    return document ? document.documentType : null;
  }

  /**
   * Replace a document - show confirmation and trigger file upload
   */
  private replaceDocument(documentType: string): void {
    const document = this.getDocumentByType(documentType);
    if (document && document.fileName) {
      this.notificationService.showReplacementConfirmation(
        documentType,
        () => {
          // User confirmed replacement
          if (document.id) {
            // Delete existing document from backend
            this.documentService.deleteDocument(document.id).subscribe({
              next: () => {
                this.removeDocument(documentType);
                this.notificationService.showInfo(
                  'Document Removed',
                  `Previous ${documentType} has been removed. Please upload a new document.`
                );
              },
              error: (error) => {
                // Even if deletion fails, allow user to upload new document
                this.removeDocument(documentType);
                const categorizedError = this.documentService.getCategorizedError(error) || 
                                        this.createFallbackError(error);
                this.notificationService.showWarning(
                  'Document Replaced',
                  `Previous ${documentType} could not be deleted from server (${categorizedError.message}), but you can upload a new document.`
                );
              }
            });
          } else {
            // No backend document to delete, just clear locally
            this.removeDocument(documentType);
          }
        }
      );
    } else {
      // No existing document, just clear any state
      this.removeDocument(documentType);
    }
  }

  removeDocument(documentType: string): void {
    // Clear upload progress
    this.documentService.clearUploadProgress(documentType);
    
    // Clear document from form service
    this.formService.updateDocument(documentType, {
      id: undefined,
      fileName: '',
      file: undefined,
      verificationStatus: 'pending',
      confidenceScore: undefined,
      statusColor: undefined,
      message: undefined,
      isBlurred: undefined,
      isCorrectType: undefined
    });
  }

  retryVerification(documentType: string): void {
    const document = this.getDocumentByType(documentType);
    if (document && document.id) {
      this.notificationService.showRetryConfirmation(
        documentType,
        () => {
          // User confirmed retry
          this.documentService.retryVerification(document.id!).subscribe({
            next: (result) => {
              this.updateDocumentFromResult(documentType, result);
              this.notificationService.showInfo(
                'Verification Retried',
                `${documentType} verification has been restarted.`
              );
            },
            error: (error) => {
              const categorizedError = this.documentService.getCategorizedError(error) || 
                                      this.createFallbackError(error);
              
              if (this.documentService.isRecoverableError(error)) {
                this.notificationService.showNotificationWithActions(
                  'error',
                  'Retry Failed',
                  `${categorizedError.message} Please try again.`,
                  [
                    {
                      label: 'Try Again',
                      action: () => this.retryVerification(documentType),
                      style: 'primary'
                    }
                  ]
                );
              } else {
                this.notificationService.showNotificationWithActions(
                  'error',
                  'Retry Failed',
                  `${categorizedError.message} Please upload a new document.`,
                  [
                    {
                      label: 'Upload New Document',
                      action: () => this.replaceDocument(documentType),
                      style: 'primary'
                    }
                  ]
                );
              }
            }
          });
        }
      );
    }
  }

  getDocumentByType(documentType: string): DocumentInfo | null {
    return this.documents.find(d => d.documentType === documentType) || null;
  }

  isDocumentUploaded(documentType: string): boolean {
    const doc = this.getDocumentByType(documentType);
    return !!(doc && doc.fileName);
  }

  isDocumentUploading(documentType: string): boolean {
    const progress = this.uploadProgress[documentType];
    return progress && (progress.status === 'uploading' || progress.status === 'processing');
  }

  /**
   * Get upload progress for a document type
   */
  getUploadProgress(documentType: string): DocumentUploadProgress | null {
    return this.uploadProgress[documentType] || null;
  }

  /**
   * Get upload progress percentage
   */
  getUploadProgressPercentage(documentType: string): number {
    const progress = this.uploadProgress[documentType];
    return progress ? progress.progress : 0;
  }

  /**
   * Get upload status message
   */
  getUploadStatusMessage(documentType: string): string {
    const progress = this.uploadProgress[documentType];
    return progress?.message || '';
  }

  getVerificationStatusClass(document: DocumentInfo): string {
    if (!document.fileName) return '';
    if (document.verificationStatus === 'pending') return 'pending';
    if (document.statusColor) return document.statusColor;
    return '';
  }

  getVerificationIcon(document: DocumentInfo): string {
    if (!document.fileName) return '';
    if (document.verificationStatus === 'pending') return '⏳';
    if (document.statusColor === 'green') return '✓';
    if (document.statusColor === 'yellow') return '⚠';
    if (document.statusColor === 'red') return '✗';
    return '';
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  getAllDocumentsValid(): boolean {
    return this.documents.every(doc => 
      doc.fileName && 
      doc.verificationStatus === 'verified' && 
      doc.statusColor !== 'red'
    );
  }

  hasAnyDocuments(): boolean {
    return this.documents.some(doc => doc.fileName);
  }

  /**
   * Helper method to accurately detect if any document is actively being processed
   * This method distinguishes between empty documents with default 'pending' status
   * and documents that are actually being uploaded or verified
   */
  private hasActiveDocumentProcessing(): boolean {
    // Check for active upload progress (uploading or processing files)
    const hasActiveUpload = Object.values(this.uploadProgress).some(progress => 
      progress.status === 'uploading' || progress.status === 'processing'
    );
    
    // Check for documents that have actual files and are pending verification
    // Only consider documents with fileName as actively being processed
    const hasActiveVerification = this.documents.some(doc => 
      doc.fileName && 
      doc.fileName.trim() !== '' && 
      doc.verificationStatus === 'pending'
    );
    
    return hasActiveUpload || hasActiveVerification;
  }

  /**
   * Enhanced method to determine if any document is currently being processed
   * Returns false for empty documents with default 'pending' status
   * Returns true only when documents are actively being uploaded or verified
   */
  isProcessingAnyDocument(): boolean {
    return this.hasActiveDocumentProcessing();
  }

  /**
   * Handle upload errors with enhanced error categorization and user guidance
   */
  private handleUploadError(error: any, documentType: string): void {
    const categorizedError = this.documentService.getCategorizedError(error) || 
                            this.createFallbackError(error);

    console.error('Upload error for', documentType, {
      originalError: error,
      categorizedError: categorizedError
    });

    // Show appropriate error message based on error type
    switch (categorizedError.type) {
      case 'azure_intelligence':
        this.handleAzureIntelligenceError(categorizedError, documentType);
        break;
      case 'network':
        this.handleNetworkError(categorizedError, documentType);
        break;
      case 'security':
        this.handleSecurityError(categorizedError, documentType);
        break;
      case 'server':
        this.handleServerError(categorizedError, documentType);
        break;
      default:
        this.handleGenericError(categorizedError, documentType);
        break;
    }

    // Clear the document from form service
    this.removeDocument(documentType);
  }

  /**
   * Handle Azure Intelligence specific errors
   */
  private handleAzureIntelligenceError(error: ErrorResponse, documentType: string): void {
    const isRecoverable = this.documentService.isRecoverableError({ categorizedError: error });
    
    if (isRecoverable) {
      this.notificationService.showNotificationWithActions(
        'error',
        'Azure Intelligence Error',
        error.message,
        [
          {
            label: 'Retry Upload',
            action: () => this.retryUpload(documentType),
            style: 'primary'
          }
        ]
      );
    } else {
      this.notificationService.showNotificationWithActions(
        'error',
        'Document Analysis Failed',
        `${error.message} ${error.suggestedAction || ''}`,
        [
          {
            label: 'Upload Different File',
            action: () => this.replaceDocument(documentType),
            style: 'primary'
          }
        ]
      );
    }
  }

  /**
   * Handle network-related errors
   */
  private handleNetworkError(error: ErrorResponse, documentType: string): void {
    this.notificationService.showNotificationWithActions(
      'error',
      'Connection Error',
      error.message,
      [
        {
          label: 'Retry Upload',
          action: () => this.retryUpload(documentType),
          style: 'primary'
        }
      ]
    );
  }

  /**
   * Handle security-related errors
   */
  private handleSecurityError(error: ErrorResponse, documentType: string): void {
    this.notificationService.showNotificationWithActions(
      'error',
      'Security Validation Failed',
      `${error.message} ${error.suggestedAction || ''}`,
      [
        {
          label: 'Upload Different File',
          action: () => this.replaceDocument(documentType),
          style: 'primary'
        }
      ]
    );
  }

  /**
   * Handle server errors
   */
  private handleServerError(error: ErrorResponse, documentType: string): void {
    const isRecoverable = this.documentService.isRecoverableError({ categorizedError: error });
    
    if (isRecoverable) {
      this.notificationService.showNotificationWithActions(
        'error',
        'Server Error',
        error.message,
        [
          {
            label: 'Retry Upload',
            action: () => this.retryUpload(documentType),
            style: 'primary'
          }
        ]
      );
    } else {
      this.notificationService.showError(
        'Upload Failed',
        `${error.message} ${error.suggestedAction || ''}`
      );
    }
  }

  /**
   * Handle generic errors
   */
  private handleGenericError(error: ErrorResponse, documentType: string): void {
    if (error.actionable) {
      this.notificationService.showNotificationWithActions(
        'error',
        'Upload Failed',
        error.message,
        [
          {
            label: 'Try Again',
            action: () => this.retryUpload(documentType),
            style: 'primary'
          }
        ]
      );
    } else {
      this.notificationService.showError('Upload Failed', error.message);
    }
  }

  /**
   * Create fallback error when categorized error is not available
   */
  private createFallbackError(error: any): ErrorResponse {
    return {
      type: 'server',
      message: error.error?.message || error.message || 'An unexpected error occurred.',
      actionable: true,
      suggestedAction: 'Please try again or contact support if the issue persists.'
    };
  }

  /**
   * Retry upload for a document type
   */
  private retryUpload(documentType: string): void {
    // Clear any existing error state
    this.removeDocument(documentType);
    
    // Show info message
    this.notificationService.showInfo(
      'Ready to Retry',
      `Please select your ${documentType} file again to retry the upload.`
    );
  }
}