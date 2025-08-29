import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DocumentVerificationComponent } from './document-verification.component';
import { ConfidenceScoreComponent } from '../confidence-score/confidence-score.component';
import { LoadingSpinnerComponent } from '../loading-spinner/loading-spinner.component';

describe('DocumentVerificationComponent', () => {
  let component: DocumentVerificationComponent;
  let fixture: ComponentFixture<DocumentVerificationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ 
        DocumentVerificationComponent,
        ConfidenceScoreComponent,
        LoadingSpinnerComponent
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DocumentVerificationComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display verification result correctly', () => {
    const verificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'verified',
      confidenceScore: 95,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'green',
      message: 'Document verified successfully',
      requiresUserConfirmation: false
    };

    component.verificationResult = verificationResult;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Document verified successfully');
  });

  it('should show loading state when verifying', () => {
    component.isLoading = true;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-loading-spinner')).toBeTruthy();
  });

  it('should show blur warning for blurred documents', () => {
    const verificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'failed',
      confidenceScore: 30,
      isBlurred: true,
      isCorrectType: true,
      statusColor: 'red',
      message: 'Document is blurred. Please upload a clear image.',
      requiresUserConfirmation: false
    };

    component.verificationResult = verificationResult;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Document is blurred');
  });

  it('should show type mismatch warning', () => {
    const verificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'failed',
      confidenceScore: 80,
      isBlurred: false,
      isCorrectType: false,
      statusColor: 'red',
      message: 'Document type does not match required type',
      requiresUserConfirmation: false
    };

    component.verificationResult = verificationResult;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Document type does not match');
  });

  it('should show confirmation dialog for low confidence', () => {
    const verificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'pending',
      confidenceScore: 65,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'yellow',
      message: 'Low confidence score. Please confirm if this is correct.',
      requiresUserConfirmation: true
    };

    component.verificationResult = verificationResult;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.confirmation-dialog')).toBeTruthy();
  });

  it('should emit confirm event when user confirms', () => {
    spyOn(component.confirmDocument, 'emit');

    component.onConfirm();

    expect(component.confirmDocument.emit).toHaveBeenCalled();
  });

  it('should emit retry event when user retries', () => {
    spyOn(component.retryVerification, 'emit');

    component.onRetry();

    expect(component.retryVerification.emit).toHaveBeenCalled();
  });

  it('should emit delete event when user deletes', () => {
    spyOn(component.deleteDocument, 'emit');

    component.onDelete();

    expect(component.deleteDocument.emit).toHaveBeenCalled();
  });

  it('should get correct status icon', () => {
    expect(component.getStatusIcon('verified')).toBe('✓');
    expect(component.getStatusIcon('failed')).toBe('✗');
    expect(component.getStatusIcon('pending')).toBe('⏳');
    expect(component.getStatusIcon('processing')).toBe('🔄');
  });

  it('should get correct status class', () => {
    expect(component.getStatusClass('green')).toBe('status-success');
    expect(component.getStatusClass('yellow')).toBe('status-warning');
    expect(component.getStatusClass('red')).toBe('status-error');
  });

  it('should show appropriate actions based on status', () => {
    // Verified document - should show delete option
    component.verificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'verified',
      confidenceScore: 95,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'green',
      message: 'Document verified',
      requiresUserConfirmation: false
    };
    fixture.detectChanges();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.delete-btn')).toBeTruthy();

    // Failed document - should show retry option
    component.verificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'failed',
      confidenceScore: 30,
      isBlurred: true,
      isCorrectType: true,
      statusColor: 'red',
      message: 'Document is blurred',
      requiresUserConfirmation: false
    };
    fixture.detectChanges();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.retry-btn')).toBeTruthy();
  });

  it('should handle missing verification result gracefully', () => {
    component.verificationResult = null;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No verification result available');
  });
});