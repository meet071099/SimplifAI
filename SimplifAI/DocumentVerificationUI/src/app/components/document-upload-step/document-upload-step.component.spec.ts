import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { of, throwError, BehaviorSubject } from 'rxjs';

import { DocumentUploadStepComponent } from './document-upload-step.component';
import { DocumentService, DocumentUploadProgress } from '../../services/document.service';
import { NotificationService } from '../../services/notification.service';
import { FormService } from '../../services/form.service';
import { FileUploadComponent } from '../shared/file-upload/file-upload.component';
import { DocumentVerificationComponent } from '../shared/document-verification/document-verification.component';
import { LoadingSpinnerComponent } from '../shared/loading-spinner/loading-spinner.component';
import { DocumentInfo, FormData } from '../../models/form.models';

describe('DocumentUploadStepComponent', () => {
  let component: DocumentUploadStepComponent;
  let fixture: ComponentFixture<DocumentUploadStepComponent>;
  let documentService: jasmine.SpyObj<DocumentService>;
  let notificationService: jasmine.SpyObj<NotificationService>;
  let formService: jasmine.SpyObj<FormService>;
  let formDataSubject: BehaviorSubject<FormData>;
  let uploadProgressSubject: BehaviorSubject<DocumentUploadProgress[]>;

  beforeEach(async () => {
    // Create mock subjects for reactive data
    formDataSubject = new BehaviorSubject<FormData>({
      id: 'test-form-id',
      personalInfo: {
        firstName: '',
        lastName: '',
        email: '',
        phone: '',
        address: '',
        dateOfBirth: null
      },
      documents: [
        {
          documentType: 'Passport',
          fileName: '',
          verificationStatus: 'pending'
        },
        {
          documentType: 'Driver License',
          fileName: '',
          verificationStatus: 'pending'
        }
      ],
      status: 'draft'
    });

    uploadProgressSubject = new BehaviorSubject<DocumentUploadProgress[]>([]);

    const documentServiceSpy = jasmine.createSpyObj('DocumentService', [
      'uploadDocument', 'getVerificationStatus', 'confirmDocument', 'deleteDocument', 
      'validateFile', 'clearUploadProgress', 'retryVerification'
    ], {
      uploadProgress$: uploadProgressSubject.asObservable(),
      verificationUpdates$: of()
    });

    const notificationServiceSpy = jasmine.createSpyObj('NotificationService', [
      'showSuccess', 'showError', 'showWarning', 'showInfo',
      'showBlurDetectionFeedback', 'showTypeMismatchFeedback', 
      'showConfidenceConfirmation', 'showReplacementConfirmation',
      'showRetryConfirmation'
    ]);

    const formServiceSpy = jasmine.createSpyObj('FormService', [
      'updateDocument', 'getCurrentFormData'
    ], {
      formData$: formDataSubject.asObservable()
    });

    await TestBed.configureTestingModule({
      declarations: [ 
        DocumentUploadStepComponent,
        FileUploadComponent,
        DocumentVerificationComponent,
        LoadingSpinnerComponent
      ],
      imports: [ HttpClientTestingModule ],
      providers: [
        { provide: DocumentService, useValue: documentServiceSpy },
        { provide: NotificationService, useValue: notificationServiceSpy },
        { provide: FormService, useValue: formServiceSpy }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DocumentUploadStepComponent);
    component = fixture.componentInstance;
    documentService = TestBed.inject(DocumentService) as jasmine.SpyObj<DocumentService>;
    notificationService = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;
    formService = TestBed.inject(FormService) as jasmine.SpyObj<FormService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('Loading State Fix Tests', () => {
    
    describe('Empty Form (No Documents) - Requirements 1.1, 1.2, 1.3', () => {
      
      it('should not show loading overlay when form has no documents', () => {
        // Arrange: Set up empty form data
        const emptyFormData: FormData = {
          id: 'test-form-id',
          personalInfo: {
            firstName: '',
            lastName: '',
            email: '',
            phone: '',
            address: '',
            dateOfBirth: null
          },
          documents: [
            {
              documentType: 'Passport',
              fileName: '',
              verificationStatus: 'pending'
            },
            {
              documentType: 'Driver License',
              fileName: '',
              verificationStatus: 'pending'
            }
          ],
          status: 'draft'
        };
        
        formDataSubject.next(emptyFormData);
        uploadProgressSubject.next([]);
        
        // Act: Initialize component
        component.ngOnInit();
        fixture.detectChanges();
        
        // Assert: No loading overlay should be shown
        expect(component.isProcessingAnyDocument()).toBe(false);
      });

      it('should return false for isProcessingAnyDocument when documents are empty with default pending status', () => {
        // Arrange: Documents with empty fileName but default 'pending' status
        component.documents = [
          {
            documentType: 'Passport',
            fileName: '',
            verificationStatus: 'pending'
          },
          {
            documentType: 'Driver License',
            fileName: '',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(false);
      });

      it('should return false for hasActiveDocumentProcessing when no documents exist', () => {
        // Arrange: Empty documents array
        component.documents = [];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(false);
      });
    });

    describe('Documents Being Uploaded - Requirements 2.1, 2.2', () => {
      
      it('should show loading overlay when document is being uploaded', () => {
        // Arrange: Set up uploading state
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {
          'Passport': {
            documentType: 'Passport',
            progress: 50,
            status: 'uploading',
            message: 'Uploading...'
          }
        };
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(true);
      });

      it('should show loading overlay when document is being processed', () => {
        // Arrange: Set up processing state
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {
          'Passport': {
            documentType: 'Passport',
            progress: 100,
            status: 'processing',
            message: 'Processing document...'
          }
        };
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(true);
      });

      it('should show loading overlay when document has fileName and is pending verification', () => {
        // Arrange: Document uploaded and pending verification
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(true);
      });
    });

    describe('Completed Documents - Requirements 2.3, 3.1', () => {
      
      it('should not show loading overlay when all documents are completed', () => {
        // Arrange: All documents verified
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'verified',
            statusColor: 'green'
          },
          {
            documentType: 'Driver License',
            fileName: 'license.jpg',
            verificationStatus: 'verified',
            statusColor: 'green'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(false);
      });

      it('should not show loading overlay when documents failed verification', () => {
        // Arrange: Documents with failed status
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'failed',
            statusColor: 'red'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(false);
      });
    });

    describe('Mixed Document States - Requirements 3.2, 3.3, 3.4', () => {
      
      it('should show loading overlay when some documents are processing and others are complete', () => {
        // Arrange: Mixed states - one processing, one complete
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'verified',
            statusColor: 'green'
          },
          {
            documentType: 'Driver License',
            fileName: 'license.jpg',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(true);
      });

      it('should show loading overlay when some documents are uploading and others are empty', () => {
        // Arrange: Mixed states - one uploading, one empty
        component.documents = [
          {
            documentType: 'Passport',
            fileName: '',
            verificationStatus: 'pending'
          },
          {
            documentType: 'Driver License',
            fileName: 'license.jpg',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {
          'Driver License': {
            documentType: 'Driver License',
            progress: 75,
            status: 'uploading',
            message: 'Uploading...'
          }
        };
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(true);
      });

      it('should not show loading overlay when all documents are either empty or complete', () => {
        // Arrange: Mixed states - some empty, some complete, none processing
        component.documents = [
          {
            documentType: 'Passport',
            fileName: '',
            verificationStatus: 'pending'
          },
          {
            documentType: 'Driver License',
            fileName: 'license.jpg',
            verificationStatus: 'verified',
            statusColor: 'green'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert
        expect(component.isProcessingAnyDocument()).toBe(false);
      });
    });

    describe('Edge Cases and Error Conditions', () => {
      
      it('should handle documents with whitespace-only fileName', () => {
        // Arrange: Document with whitespace fileName
        component.documents = [
          {
            documentType: 'Passport',
            fileName: '   ',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert: Should treat as empty document
        expect(component.isProcessingAnyDocument()).toBe(false);
      });

      it('should handle upload progress with completed status', () => {
        // Arrange: Upload progress shows completed
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'verified'
          }
        ];
        component.uploadProgress = {
          'Passport': {
            documentType: 'Passport',
            progress: 100,
            status: 'completed',
            message: 'Upload complete'
          }
        };
        
        // Act & Assert: Should not show loading for completed uploads
        expect(component.isProcessingAnyDocument()).toBe(false);
      });

      it('should handle upload progress with error status', () => {
        // Arrange: Upload progress shows error
        component.documents = [
          {
            documentType: 'Passport',
            fileName: 'passport.jpg',
            verificationStatus: 'failed'
          }
        ];
        component.uploadProgress = {
          'Passport': {
            documentType: 'Passport',
            progress: 0,
            status: 'error',
            message: 'Upload failed'
          }
        };
        
        // Act & Assert: Should not show loading for error states
        expect(component.isProcessingAnyDocument()).toBe(false);
      });

      it('should handle undefined or null fileName', () => {
        // Arrange: Document with undefined fileName
        component.documents = [
          {
            documentType: 'Passport',
            fileName: undefined as any,
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {};
        
        // Act & Assert: Should treat as empty document
        expect(component.isProcessingAnyDocument()).toBe(false);
      });
    });

    describe('Integration with Component Lifecycle', () => {
      
      it('should properly initialize loading state on component init with empty form', () => {
        // Arrange: Empty form data
        const emptyFormData: FormData = {
          id: 'test-form-id',
          personalInfo: {
            firstName: '',
            lastName: '',
            email: '',
            phone: '',
            address: '',
            dateOfBirth: null
          },
          documents: [
            {
              documentType: 'Passport',
              fileName: '',
              verificationStatus: 'pending'
            }
          ],
          status: 'draft'
        };
        
        formDataSubject.next(emptyFormData);
        uploadProgressSubject.next([]);
        
        // Act: Initialize component
        component.ngOnInit();
        fixture.detectChanges();
        
        // Assert: Should not be processing
        expect(component.isProcessingAnyDocument()).toBe(false);
        expect(component.documents.length).toBe(1);
        expect(component.documents[0].fileName).toBe('');
      });

      it('should update loading state when upload progress changes', () => {
        // Arrange: Start with empty state
        component.documents = [
          {
            documentType: 'Passport',
            fileName: '',
            verificationStatus: 'pending'
          }
        ];
        component.uploadProgress = {};
        
        expect(component.isProcessingAnyDocument()).toBe(false);
        
        // Act: Simulate upload progress update
        uploadProgressSubject.next([
          {
            documentType: 'Passport',
            progress: 50,
            status: 'uploading',
            message: 'Uploading...'
          }
        ]);
        
        // Update component state as it would happen in real scenario
        component.uploadProgress = {
          'Passport': {
            documentType: 'Passport',
            progress: 50,
            status: 'uploading',
            message: 'Uploading...'
          }
        };
        
        // Assert: Should now be processing
        expect(component.isProcessingAnyDocument()).toBe(true);
      });
    });
  });
});