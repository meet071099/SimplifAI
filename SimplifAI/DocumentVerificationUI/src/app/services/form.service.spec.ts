import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FormService } from './form.service';
import { FormData, PersonalInfo, DocumentInfo } from '../models/form.models';
import { environment } from '../../environments/environment';

describe('FormService', () => {
  let service: FormService;
  let httpMock: HttpTestingController;

  const mockPersonalInfo: PersonalInfo = {
    firstName: 'John',
    lastName: 'Doe',
    email: 'john@example.com',
    phone: '123-456-7890',
    address: '123 Main St',
    dateOfBirth: new Date('1990-01-01')
  };

  const mockFormData: FormData = {
    id: 'test-form-id',
    personalInfo: mockPersonalInfo,
    documents: [],
    status: 'pending',
    createdAt: new Date(),
    submittedAt: null
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FormService]
    });
    service = TestBed.inject(FormService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should initialize form data', () => {
    service.initializeForm('test-form-id').subscribe(formData => {
      expect(formData).toEqual(mockFormData);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/forms/test-form-id/initialize`);
    expect(req.request.method).toBe('POST');
    req.flush(mockFormData);
  });

  it('should update personal info', () => {
    service.updatePersonalInfo(mockPersonalInfo).subscribe(result => {
      expect(result).toEqual(mockPersonalInfo);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/forms/personal-info`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(mockPersonalInfo);
    req.flush(mockPersonalInfo);
  });

  it('should submit form', () => {
    const mockResponse = {
      formId: 'test-form-id',
      status: 'submitted',
      submittedAt: new Date(),
      documents: []
    };

    service.submitForm('test-form-id').subscribe(result => {
      expect(result).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/api/forms/test-form-id/submit`);
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('should validate personal info step', () => {
    // Set form data with valid personal info
    service.setFormData(mockFormData);

    const validation = service.validatePersonalInfoStep();
    expect(validation.isValid).toBe(true);
    expect(validation.errors).toEqual([]);
  });

  it('should validate personal info step with missing data', () => {
    const invalidFormData = {
      ...mockFormData,
      personalInfo: {
        ...mockPersonalInfo,
        firstName: '',
        email: 'invalid-email'
      }
    };
    
    service.setFormData(invalidFormData);

    const validation = service.validatePersonalInfoStep();
    expect(validation.isValid).toBe(false);
    expect(validation.errors.length).toBeGreaterThan(0);
  });

  it('should validate document upload step', () => {
    const formDataWithDocuments = {
      ...mockFormData,
      documents: [
        {
          id: 'doc-1',
          documentType: 'Passport',
          fileName: 'passport.jpg',
          verificationStatus: 'verified',
          confidenceScore: 95,
          statusColor: 'green'
        }
      ]
    };
    
    service.setFormData(formDataWithDocuments);

    const validation = service.validateDocumentUploadStep();
    expect(validation.isValid).toBe(true);
  });

  it('should validate review step', () => {
    const completeFormData = {
      ...mockFormData,
      documents: [
        {
          id: 'doc-1',
          documentType: 'Passport',
          fileName: 'passport.jpg',
          verificationStatus: 'verified',
          confidenceScore: 95,
          statusColor: 'green'
        }
      ]
    };
    
    service.setFormData(completeFormData);

    const validation = service.validateReviewStep();
    expect(validation.isValid).toBe(true);
  });

  it('should set and get current step', () => {
    service.setCurrentStep(2);
    
    service.currentStep$.subscribe(step => {
      expect(step).toBe(2);
    });
  });

  it('should set and get form data', () => {
    service.setFormData(mockFormData);
    
    service.formData$.subscribe(data => {
      expect(data).toEqual(mockFormData);
    });
  });

  it('should add document to form data', () => {
    const document: DocumentInfo = {
      id: 'doc-1',
      documentType: 'Passport',
      fileName: 'passport.jpg',
      verificationStatus: 'pending',
      confidenceScore: null,
      statusColor: 'yellow'
    };

    service.setFormData(mockFormData);
    service.addDocument(document);

    service.formData$.subscribe(data => {
      expect(data?.documents.length).toBe(1);
      expect(data?.documents[0]).toEqual(document);
    });
  });

  it('should update document in form data', () => {
    const document: DocumentInfo = {
      id: 'doc-1',
      documentType: 'Passport',
      fileName: 'passport.jpg',
      verificationStatus: 'pending',
      confidenceScore: null,
      statusColor: 'yellow'
    };

    const updatedDocument: DocumentInfo = {
      ...document,
      verificationStatus: 'verified',
      confidenceScore: 95,
      statusColor: 'green'
    };

    const formDataWithDoc = {
      ...mockFormData,
      documents: [document]
    };

    service.setFormData(formDataWithDoc);
    service.updateDocument(updatedDocument);

    service.formData$.subscribe(data => {
      expect(data?.documents[0]).toEqual(updatedDocument);
    });
  });

  it('should remove document from form data', () => {
    const document: DocumentInfo = {
      id: 'doc-1',
      documentType: 'Passport',
      fileName: 'passport.jpg',
      verificationStatus: 'verified',
      confidenceScore: 95,
      statusColor: 'green'
    };

    const formDataWithDoc = {
      ...mockFormData,
      documents: [document]
    };

    service.setFormData(formDataWithDoc);
    service.removeDocument('doc-1');

    service.formData$.subscribe(data => {
      expect(data?.documents.length).toBe(0);
    });
  });

  it('should get form progress', () => {
    const formDataWithDocuments = {
      ...mockFormData,
      documents: [
        {
          id: 'doc-1',
          documentType: 'Passport',
          fileName: 'passport.jpg',
          verificationStatus: 'verified',
          confidenceScore: 95,
          statusColor: 'green'
        }
      ]
    };
    
    service.setFormData(formDataWithDocuments);

    const progress = service.getFormProgress();
    expect(progress.personalInfoComplete).toBe(true);
    expect(progress.documentsComplete).toBe(true);
    expect(progress.overallProgress).toBe(100);
  });

  it('should reset form data', () => {
    service.setFormData(mockFormData);
    service.resetForm();

    service.formData$.subscribe(data => {
      expect(data).toBeNull();
    });

    service.currentStep$.subscribe(step => {
      expect(step).toBe(1);
    });
  });
});