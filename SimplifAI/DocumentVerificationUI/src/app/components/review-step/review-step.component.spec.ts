import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { ReviewStepComponent } from './review-step.component';
import { FormService } from '../../services/form.service';
import { NotificationService } from '../../services/notification.service';
import { ConfidenceScoreComponent } from '../shared/confidence-score/confidence-score.component';

describe('ReviewStepComponent', () => {
  let component: ReviewStepComponent;
  let fixture: ComponentFixture<ReviewStepComponent>;
  let formService: jasmine.SpyObj<FormService>;
  let notificationService: jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    const formServiceSpy = jasmine.createSpyObj('FormService', ['submitForm']);
    const notificationServiceSpy = jasmine.createSpyObj('NotificationService', [
      'showSuccess', 'showError', 'showWarning'
    ]);

    await TestBed.configureTestingModule({
      declarations: [ 
        ReviewStepComponent,
        ConfidenceScoreComponent
      ],
      imports: [ HttpClientTestingModule ],
      providers: [
        { provide: FormService, useValue: formServiceSpy },
        { provide: NotificationService, useValue: notificationServiceSpy }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReviewStepComponent);
    component = fixture.componentInstance;
    formService = TestBed.inject(FormService) as jasmine.SpyObj<FormService>;
    notificationService = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default values', () => {
    expect(component.isSubmitting).toBe(false);
    expect(component.showConfirmDialog).toBe(false);
  });

  it('should display personal information correctly', () => {
    const personalInfo = {
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      phone: '123-456-7890',
      address: '123 Main St',
      dateOfBirth: new Date('1990-01-01')
    };

    component.personalInfo = personalInfo;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('John');
    expect(compiled.textContent).toContain('Doe');
    expect(compiled.textContent).toContain('john@example.com');
  });

  it('should display uploaded documents correctly', () => {
    const documents = {
      'Passport': {
        documentId: 'doc-1',
        verificationStatus: 'verified',
        confidenceScore: 95,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'green',
        message: 'Document verified',
        requiresUserConfirmation: false
      },
      'Driver License': {
        documentId: 'doc-2',
        verificationStatus: 'verified',
        confidenceScore: 85,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'green',
        message: 'Document verified',
        requiresUserConfirmation: false
      }
    };

    component.uploadedDocuments = documents;
    fixture.detectChanges();

    expect(component.getDocumentEntries().length).toBe(2);
    expect(component.getDocumentEntries()[0][0]).toBe('Passport');
    expect(component.getDocumentEntries()[1][0]).toBe('Driver License');
  });

  it('should show confirmation dialog on submit', () => {
    component.onSubmit();

    expect(component.showConfirmDialog).toBe(true);
  });

  it('should cancel submission', () => {
    component.showConfirmDialog = true;

    component.cancelSubmission();

    expect(component.showConfirmDialog).toBe(false);
  });

  it('should confirm submission and emit submit event', () => {
    spyOn(component.submitForm, 'emit');

    component.confirmSubmission();

    expect(component.showConfirmDialog).toBe(false);
    expect(component.submitForm.emit).toHaveBeenCalled();
  });

  it('should check if all information is complete', () => {
    // Incomplete - no personal info
    expect(component.isAllInformationComplete()).toBe(false);

    // Add personal info
    component.personalInfo = {
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      phone: '123-456-7890',
      address: '123 Main St',
      dateOfBirth: new Date('1990-01-01')
    };

    // Still incomplete - no documents
    expect(component.isAllInformationComplete()).toBe(false);

    // Add documents
    component.uploadedDocuments = {
      'Passport': {
        documentId: 'doc-1',
        verificationStatus: 'verified',
        confidenceScore: 95,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'green',
        message: 'Document verified',
        requiresUserConfirmation: false
      }
    };

    // Now complete
    expect(component.isAllInformationComplete()).toBe(true);
  });

  it('should format date correctly', () => {
    const date = new Date('1990-01-01');
    const formatted = component.formatDate(date);
    expect(formatted).toBe('January 1, 1990');
  });

  it('should get document status class correctly', () => {
    const greenDoc = {
      documentId: 'doc-1',
      verificationStatus: 'verified',
      confidenceScore: 95,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'green',
      message: 'Document verified',
      requiresUserConfirmation: false
    };

    const yellowDoc = {
      ...greenDoc,
      statusColor: 'yellow',
      confidenceScore: 75
    };

    const redDoc = {
      ...greenDoc,
      statusColor: 'red',
      confidenceScore: 45
    };

    expect(component.getDocumentStatusClass(greenDoc)).toBe('status-green');
    expect(component.getDocumentStatusClass(yellowDoc)).toBe('status-yellow');
    expect(component.getDocumentStatusClass(redDoc)).toBe('status-red');
  });

  it('should get confidence score text correctly', () => {
    const doc = {
      documentId: 'doc-1',
      verificationStatus: 'verified',
      confidenceScore: 95,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'green',
      message: 'Document verified',
      requiresUserConfirmation: false
    };

    expect(component.getConfidenceScoreText(doc)).toBe('95% confidence');

    const docWithoutScore = { ...doc, confidenceScore: undefined };
    expect(component.getConfidenceScoreText(docWithoutScore)).toBe('Processing...');
  });
});