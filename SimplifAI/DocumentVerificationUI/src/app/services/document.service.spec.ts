import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DocumentService, DocumentUploadRequest, DocumentVerificationResult } from './document.service';
import { environment } from '../../environments/environment';

describe('DocumentService', () => {
  let service: DocumentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [DocumentService]
    });
    service = TestBed.inject(DocumentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should validate file correctly', () => {
    // Test valid file
    const validFile = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const validResult = service.validateFile(validFile);
    expect(validResult.isValid).toBe(true);

    // Test invalid file type
    const invalidFile = new File(['test'], 'test.txt', { type: 'text/plain' });
    const invalidResult = service.validateFile(invalidFile);
    expect(invalidResult.isValid).toBe(false);
    expect(invalidResult.error).toContain('File type not supported');

    // Test file too large
    const largeFile = new File([new ArrayBuffer(11 * 1024 * 1024)], 'large.jpg', { type: 'image/jpeg' });
    const largeResult = service.validateFile(largeFile);
    expect(largeResult.isValid).toBe(false);
    expect(largeResult.error).toContain('File size must be less than');
  });

  it('should upload document and track progress', (done) => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const uploadRequest: DocumentUploadRequest = {
      file,
      documentType: 'Passport',
      formId: 'test-form-id'
    };

    const mockResponse: DocumentVerificationResult = {
      documentId: 'doc-123',
      verificationStatus: 'pending',
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'yellow',
      message: 'Processing document...',
      requiresUserConfirmation: false
    };

    service.uploadDocument(uploadRequest).subscribe(result => {
      if (result) {
        expect(result).toEqual(mockResponse);
        done();
      }
    });

    // Verify the HTTP request
    const req = httpMock.expectOne(`${environment.apiUrl}/api/documents/upload`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeInstanceOf(FormData);
    
    // Simulate successful response with proper event structure
    req.event({
      type: 4, // HttpEventType.Response
      body: mockResponse
    } as any);
  });

  it('should get verification status', () => {
    const documentId = 'doc-123';
    const mockResult: DocumentVerificationResult = {
      documentId,
      verificationStatus: 'verified',
      confidenceScore: 95,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'green',
      message: 'Document verified successfully',
      requiresUserConfirmation: false
    };

    service.getVerificationStatus(documentId).subscribe(result => {
      expect(result).toEqual(mockResult);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/documents/${documentId}/status`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should confirm document', () => {
    const documentId = 'doc-123';
    const mockResult: DocumentVerificationResult = {
      documentId,
      verificationStatus: 'verified',
      confidenceScore: 75,
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'green',
      message: 'Document confirmed by user',
      requiresUserConfirmation: false
    };

    service.confirmDocument(documentId).subscribe(result => {
      expect(result).toEqual(mockResult);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/documents/${documentId}/confirm`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResult);
  });

  it('should delete document', () => {
    const documentId = 'doc-123';

    service.deleteDocument(documentId).subscribe(result => {
      expect(result).toBeNull();
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/documents/${documentId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should retry verification', () => {
    const documentId = 'doc-123';
    const mockResult: DocumentVerificationResult = {
      documentId,
      verificationStatus: 'pending',
      isBlurred: false,
      isCorrectType: true,
      statusColor: 'yellow',
      message: 'Retrying verification...',
      requiresUserConfirmation: false
    };

    service.retryVerification(documentId).subscribe(result => {
      expect(result).toEqual(mockResult);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/documents/${documentId}/retry`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResult);
  });

  it('should track upload progress', () => {
    const documentType = 'Passport';
    
    // Initially no progress
    expect(service.getUploadProgress(documentType)).toBeNull();

    // Subscribe to progress updates
    let progressUpdates: any[] = [];
    service.uploadProgress$.subscribe(progress => {
      progressUpdates = progress;
    });

    // Simulate progress update (this would normally be done internally)
    service['updateUploadProgress'](documentType, 50, 'uploading', 'Uploading... 50%');
    
    expect(progressUpdates.length).toBe(1);
    expect(progressUpdates[0].documentType).toBe(documentType);
    expect(progressUpdates[0].progress).toBe(50);
    expect(progressUpdates[0].status).toBe('uploading');

    // Clear progress
    service.clearUploadProgress(documentType);
    expect(service.getUploadProgress(documentType)).toBeNull();
  });

  describe('Document ID Format Validation', () => {
    it('should validate valid GUID format', () => {
      const validGuids = [
        '12345678-1234-1234-1234-123456789abc',
        'ABCDEF12-3456-7890-ABCD-EF1234567890',
        'a1b2c3d4-e5f6-7890-1234-567890abcdef',
        '00000000-0000-0000-0000-000000000000'
      ];

      validGuids.forEach(guid => {
        const result = service.validateDocumentIdFormat(guid);
        expect(result.isValid).toBe(true);
        expect(result.error).toBeUndefined();
      });
    });

    it('should reject invalid GUID formats', () => {
      const invalidGuids = [
        { id: '', expectedError: 'Document ID cannot be empty or contain only whitespace.' },
        { id: '   ', expectedError: 'Document ID cannot be empty or contain only whitespace.' },
        { id: '12345678-1234-1234-1234-123456789ab', expectedError: 'Document ID must be 36 characters long (GUID format). Received: 35 characters.' },
        { id: '12345678-1234-1234-1234-123456789abcd', expectedError: 'Document ID must be 36 characters long (GUID format). Received: 37 characters.' },
        { id: '12345678-1234-1234-1234-123456789abg', expectedError: 'Document ID must be in valid GUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).' },
        { id: '12345678_1234_1234_1234_123456789abc', expectedError: 'Document ID must be in valid GUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).' },
        { id: '123456781234123412341234567890abc', expectedError: 'Document ID must be in valid GUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).' },
        { id: 'not-a-guid-at-all', expectedError: 'Document ID must be 36 characters long (GUID format). Received: 17 characters.' }
      ];

      invalidGuids.forEach(({ id, expectedError }) => {
        const result = service.validateDocumentIdFormat(id);
        expect(result.isValid).toBe(false);
        expect(result.error).toBe(expectedError);
      });
    });

    it('should handle null and undefined document IDs', () => {
      const nullResult = service.validateDocumentIdFormat(null as any);
      expect(nullResult.isValid).toBe(false);
      expect(nullResult.error).toBe('Document ID is required and cannot be null or undefined.');

      const undefinedResult = service.validateDocumentIdFormat(undefined as any);
      expect(undefinedResult.isValid).toBe(false);
      expect(undefinedResult.error).toBe('Document ID is required and cannot be null or undefined.');
    });

    it('should handle non-string document IDs', () => {
      const nonStringIds = [123, {}, [], true, false];

      nonStringIds.forEach(id => {
        const result = service.validateDocumentIdFormat(id as any);
        expect(result.isValid).toBe(false);
        expect(result.error).toBe('Document ID must be a string.');
      });
    });

    it('should trim whitespace from document IDs', () => {
      const guidWithWhitespace = '  12345678-1234-1234-1234-123456789abc  ';
      const result = service.validateDocumentIdFormat(guidWithWhitespace);
      expect(result.isValid).toBe(true);
      expect(result.error).toBeUndefined();
    });
  });

  describe('Document Existence Validation', () => {
    it('should track uploaded documents', () => {
      const documentId = '12345678-1234-1234-1234-123456789abc';
      
      // Initially document should not be tracked
      expect(service.isDocumentTracked(documentId)).toBe(false);

      // Simulate document upload by calling the private method
      service['trackUploadedDocument'](documentId);

      // Now document should be tracked
      expect(service.isDocumentTracked(documentId)).toBe(true);
    });

    it('should not track documents with invalid IDs', () => {
      const invalidDocumentId = 'invalid-id';
      
      // Try to track invalid document ID
      service['trackUploadedDocument'](invalidDocumentId);

      // Should not be tracked due to invalid format
      expect(service.isDocumentTracked(invalidDocumentId)).toBe(false);
    });

    it('should untrack documents', () => {
      const documentId = '12345678-1234-1234-1234-123456789abc';
      
      // Track document
      service['trackUploadedDocument'](documentId);
      expect(service.isDocumentTracked(documentId)).toBe(true);

      // Untrack document
      service['untrackUploadedDocument'](documentId);
      expect(service.isDocumentTracked(documentId)).toBe(false);
    });

    it('should validate document for status request - valid case', () => {
      const documentId = '12345678-1234-1234-1234-123456789abc';
      
      // Track the document first
      service['trackUploadedDocument'](documentId);

      // Validate for status request
      const result = service['validateDocumentForStatusRequest'](documentId);
      expect(result.isValid).toBe(true);
      expect(result.error).toBeUndefined();
    });

    it('should validate document for status request - invalid format', () => {
      const invalidDocumentId = 'invalid-id';

      const result = service['validateDocumentForStatusRequest'](invalidDocumentId);
      expect(result.isValid).toBe(false);
      expect(result.error).toContain('Document ID must be 36 characters long');
    });

    it('should validate document for status request - not uploaded', () => {
      const documentId = '12345678-1234-1234-1234-123456789abc';
      
      // Don't track the document, so it appears as not uploaded
      const result = service['validateDocumentForStatusRequest'](documentId);
      expect(result.isValid).toBe(false);
      expect(result.error).toBe('Document not found in uploaded documents. The document may not have been uploaded successfully or the ID is incorrect.');
    });

    it('should get tracked document IDs', () => {
      const documentIds = [
        '12345678-1234-1234-1234-123456789abc',
        '87654321-4321-4321-4321-cba987654321'
      ];

      // Initially no documents tracked
      expect(service.getTrackedDocumentIds()).toEqual([]);

      // Track some documents
      documentIds.forEach(id => service['trackUploadedDocument'](id));

      // Should return all tracked document IDs
      const trackedIds = service.getTrackedDocumentIds();
      expect(trackedIds.length).toBe(2);
      expect(trackedIds).toContain(documentIds[0]);
      expect(trackedIds).toContain(documentIds[1]);
    });

    it('should clear tracked documents', () => {
      const documentIds = [
        '12345678-1234-1234-1234-123456789abc',
        '87654321-4321-4321-4321-cba987654321'
      ];

      // Track some documents
      documentIds.forEach(id => service['trackUploadedDocument'](id));
      expect(service.getTrackedDocumentIds().length).toBe(2);

      // Clear all tracked documents
      service.clearTrackedDocuments();
      expect(service.getTrackedDocumentIds()).toEqual([]);
    });

    it('should validate document for status check - comprehensive validation', () => {
      const validDocumentId = '12345678-1234-1234-1234-123456789abc';
      const invalidDocumentId = 'invalid-id';

      // Test with invalid format
      const invalidResult = service.validateDocumentForStatusCheck(invalidDocumentId);
      expect(invalidResult.canCheckStatus).toBe(false);
      expect(invalidResult.error).toContain('Document ID must be 36 characters long');
      expect(invalidResult.validationDetails.hasValidFormat).toBe(false);
      expect(invalidResult.validationDetails.isTracked).toBe(false);

      // Test with valid format but not tracked
      const notTrackedResult = service.validateDocumentForStatusCheck(validDocumentId);
      expect(notTrackedResult.canCheckStatus).toBe(false);
      expect(notTrackedResult.error).toBe('Document not found in uploaded documents. Please ensure the document was uploaded successfully before checking status.');
      expect(notTrackedResult.validationDetails.hasValidFormat).toBe(true);
      expect(notTrackedResult.validationDetails.isTracked).toBe(false);

      // Track the document and test again
      service['trackUploadedDocument'](validDocumentId);
      const validResult = service.validateDocumentForStatusCheck(validDocumentId);
      expect(validResult.canCheckStatus).toBe(true);
      expect(validResult.error).toBeUndefined();
      expect(validResult.validationDetails.hasValidFormat).toBe(true);
      expect(validResult.validationDetails.isTracked).toBe(true);
    });
  });
});