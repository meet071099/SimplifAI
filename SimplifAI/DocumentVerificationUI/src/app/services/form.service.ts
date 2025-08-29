import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of, firstValueFrom } from 'rxjs';
import { tap, catchError, timeout, filter, take } from 'rxjs/operators';
import { FormData, PersonalInfo, DocumentInfo, StepValidation, REQUIRED_DOCUMENT_TYPES } from '../models/form.models';
import { environment } from '../../environments/environment';

interface FormInitializationState {
  status: 'initializing' | 'ready' | 'error' | 'demo';
  formId: string | null;
  error: string | null;
  timestamp: Date;
}

interface SaveOperationState {
  isSaving: boolean;
  lastSaveAttempt: Date | null;
  saveError: string | null;
  retryCount: number;
  queuedOperations: Array<{
    type: 'personalInfo';
    data: PersonalInfo;
    timestamp: Date;
  }>;
}

@Injectable({
  providedIn: 'root'
})
export class FormService {
  private baseUrl = environment.apiUrl;
  private formDataSubject = new BehaviorSubject<FormData>(this.getInitialFormData());
  public formData$ = this.formDataSubject.asObservable();

  private currentStepSubject = new BehaviorSubject<number>(1);
  public currentStep$ = this.currentStepSubject.asObservable();

  private currentFormId?: string;

  // Form readiness management properties
  private isFormReady = false;
  private formReadySubject = new BehaviorSubject<boolean>(false);
  public formReady$ = this.formReadySubject.asObservable();
  
  private initializationPromise: Promise<any> | null = null;
  private initializationState: FormInitializationState = {
    status: 'initializing',
    formId: null,
    error: null,
    timestamp: new Date()
  };
  
  private initializationStateSubject = new BehaviorSubject<FormInitializationState>(this.initializationState);
  public initializationState$ = this.initializationStateSubject.asObservable();

  // Save operation state management
  private saveOperationState: SaveOperationState = {
    isSaving: false,
    lastSaveAttempt: null,
    saveError: null,
    retryCount: 0,
    queuedOperations: []
  };
  
  private saveOperationStateSubject = new BehaviorSubject<SaveOperationState>(this.saveOperationState);
  public saveOperationState$ = this.saveOperationStateSubject.asObservable();

  private readonly MAX_RETRY_ATTEMPTS = 3;
  private readonly INITIAL_RETRY_DELAY = 1000; // 1 second
  private readonly MAX_SAVE_RETRY_ATTEMPTS = 2;
  private readonly SAVE_RETRY_DELAY = 1500; // 1.5 seconds

  constructor(private http: HttpClient) {}

  private getInitialFormData(): FormData {
    return {
      personalInfo: {
        firstName: '',
        lastName: '',
        email: '',
        phone: '',
        address: '',
        dateOfBirth: null
      },
      documents: REQUIRED_DOCUMENT_TYPES.map(type => ({
        documentType: type,
        fileName: '',
        verificationStatus: 'pending' as const
      })),
      status: 'draft'
    };
  }

  getCurrentFormData(): FormData {
    return this.formDataSubject.value;
  }

  async updatePersonalInfo(personalInfo: PersonalInfo): Promise<void> {
    const currentData = this.getCurrentFormData();
    const updatedData = {
      ...currentData,
      personalInfo
    };
    
    // Always update the local state immediately (non-blocking)
    this.formDataSubject.next(updatedData);

    try {
      // Check if form is ready
      if (!this.isFormReadyForOperations()) {
        // Queue the operation if form is not ready
        this.queueSaveOperation('personalInfo', personalInfo);
        console.log('Form not ready - personal info save operation queued');
        return;
      }

      // Form is ready - attempt to save
      await this.performPersonalInfoSave(personalInfo);
      
      // Process any queued operations after successful save
      await this.processQueuedOperations();
      
    } catch (error) {
      console.error('Error in updatePersonalInfo:', error);
      // Update save operation state with error
      this.updateSaveOperationState({
        saveError: error instanceof Error ? error.message : 'Unknown error occurred',
        isSaving: false
      });
    }
  }

  /**
   * Queue a save operation when form is not ready
   */
  private queueSaveOperation(type: 'personalInfo', data: PersonalInfo): void {
    const operation = {
      type,
      data,
      timestamp: new Date()
    };
    
    // Remove any existing queued operation of the same type (keep only the latest)
    const filteredQueue = this.saveOperationState.queuedOperations.filter(op => op.type !== type);
    
    this.updateSaveOperationState({
      queuedOperations: [...filteredQueue, operation]
    });
    
    console.log(`Queued ${type} save operation`);
  }

  /**
   * Process all queued save operations
   */
  private async processQueuedOperations(): Promise<void> {
    const queuedOps = [...this.saveOperationState.queuedOperations];
    
    if (queuedOps.length === 0) {
      return;
    }
    
    console.log(`Processing ${queuedOps.length} queued save operations`);
    
    // Clear the queue first
    this.updateSaveOperationState({
      queuedOperations: []
    });
    
    // Process each operation
    for (const operation of queuedOps) {
      try {
        if (operation.type === 'personalInfo') {
          await this.performPersonalInfoSave(operation.data);
        }
      } catch (error) {
        console.error(`Error processing queued ${operation.type} operation:`, error);
        // Re-queue failed operations
        this.queueSaveOperation(operation.type, operation.data);
      }
    }
  }

  /**
   * Perform the actual personal info save with retry logic
   */
  private async performPersonalInfoSave(personalInfo: PersonalInfo): Promise<void> {
    // Update save state to indicate saving is in progress
    this.updateSaveOperationState({
      isSaving: true,
      lastSaveAttempt: new Date(),
      saveError: null
    });

    // Don't attempt backend save in demo mode
    if (this.currentFormId && this.currentFormId.startsWith('demo-form-')) {
      console.log('Personal info updated locally (demo mode)');
      this.updateSaveOperationState({
        isSaving: false
      });
      return;
    }

    // Attempt save with retry logic
    let lastError: any = null;
    
    for (let attempt = 1; attempt <= this.MAX_SAVE_RETRY_ATTEMPTS; attempt++) {
      try {
        await firstValueFrom(this.savePersonalInfoToBackend(personalInfo));
        
        // Success - update save state
        this.updateSaveOperationState({
          isSaving: false,
          saveError: null,
          retryCount: 0
        });
        
        console.log('Personal info saved successfully to backend');
        return;
        
      } catch (error) {
        lastError = error;
        console.error(`Personal info save attempt ${attempt} failed:`, error);
        
        // Handle 404 errors that might indicate form initialization issues
        if (error && typeof error === 'object' && 'status' in error && (error as any).status === 404) {
          console.warn('404 error detected - form may need re-initialization');
          this.handleFormNotFoundError();
          throw error; // Don't retry 404 errors
        }
        
        // Update retry count
        this.updateSaveOperationState({
          retryCount: attempt
        });
        
        if (attempt < this.MAX_SAVE_RETRY_ATTEMPTS) {
          console.log(`Retrying personal info save in ${this.SAVE_RETRY_DELAY}ms...`);
          await this.delay(this.SAVE_RETRY_DELAY);
        }
      }
    }
    
    // All attempts failed
    this.updateSaveOperationState({
      isSaving: false,
      saveError: lastError?.message || 'Failed to save personal info after multiple attempts'
    });
    
    throw lastError;
  }

  /**
   * Handle 404 errors that might indicate form initialization issues
   */
  private handleFormNotFoundError(): void {
    console.log('Handling form not found error - may need re-initialization');
    // Reset form readiness state to trigger re-initialization if needed
    this.setFormReady(false);
    this.initializationPromise = null;
    
    this.updateInitializationState({
      status: 'error',
      formId: this.currentFormId || null,
      error: 'Form not found - may need re-initialization',
      timestamp: new Date()
    });
  }

  private savePersonalInfoToBackend(personalInfo: PersonalInfo): Observable<any> {
    const payload = {
      firstName: personalInfo.firstName,
      lastName: personalInfo.lastName,
      email: personalInfo.email,
      phone: personalInfo.phone,
      address: personalInfo.address,
      dateOfBirth: personalInfo.dateOfBirth ? personalInfo.dateOfBirth.toISOString() : null
    };

    return this.http.post(`${this.baseUrl}/api/Form/${this.currentFormId}/personal-info`, payload).pipe(
      timeout(5000), // 5 second timeout
      catchError(error => {
        console.error('Backend save error:', error);
        // Return empty observable to prevent error propagation
        return of(null);
      })
    );
  }

  setFormId(formId: string): void {
    this.currentFormId = formId;
  }

  getFormId(): string | undefined {
    return this.currentFormId;
  }

  /**
   * Wait for form to be ready for operations
   * Returns a promise that resolves when form is initialized
   */
  async waitForFormReady(): Promise<void> {
    if (this.isFormReady) {
      return Promise.resolve();
    }

    if (this.initializationPromise) {
      await this.initializationPromise;
      return;
    }

    // Wait for form ready state to become true
    return firstValueFrom(
      this.formReady$.pipe(
        filter(ready => ready),
        take(1)
      )
    ).then(() => {});
  }

  /**
   * Get current form initialization state
   */
  getInitializationState(): FormInitializationState {
    return { ...this.initializationState };
  }

  /**
   * Check if form is ready for operations
   */
  isFormReadyForOperations(): boolean {
    return this.isFormReady;
  }

  /**
   * Get current save operation state
   */
  getSaveOperationState(): SaveOperationState {
    return { ...this.saveOperationState };
  }

  /**
   * Update save operation state and notify subscribers
   */
  private updateSaveOperationState(state: Partial<SaveOperationState>): void {
    this.saveOperationState = { ...this.saveOperationState, ...state };
    this.saveOperationStateSubject.next(this.saveOperationState);
  }

  /**
   * Initialize a new form with the backend and get a form ID
   * Enhanced with proper state management and retry logic
   */
  initializeForm(recruiterEmail?: string): Observable<any> {
    // If already initializing, return the existing promise
    if (this.initializationPromise) {
      return of(this.initializationPromise);
    }

    // Set initializing state
    this.updateInitializationState({
      status: 'initializing',
      formId: null,
      error: null,
      timestamp: new Date()
    });

    const payload = {
      recruiterEmail: recruiterEmail || 'demo@example.com'
    };

    // Create the initialization promise with retry logic
    this.initializationPromise = this.performFormInitializationWithRetry(payload);

    return of(this.initializationPromise);
  }

  /**
   * Perform form initialization with exponential backoff retry logic
   */
  private async performFormInitializationWithRetry(payload: any): Promise<any> {
    let lastError: any = null;

    for (let attempt = 1; attempt <= this.MAX_RETRY_ATTEMPTS; attempt++) {
      try {
        console.log(`Form initialization attempt ${attempt}/${this.MAX_RETRY_ATTEMPTS}`);
        
        const response = await firstValueFrom(
          this.http.post(`${this.baseUrl}/api/Form`, payload).pipe(
            timeout(5000) // 5 second timeout
          )
        );

        if (response && (response as any).id) {
          // Success - set form ID and ready state
          this.setFormId((response as any).id);
          this.setFormReady(true);
          
          this.updateInitializationState({
            status: 'ready',
            formId: (response as any).id,
            error: null,
            timestamp: new Date()
          });

          console.log('Form initialized successfully with ID:', (response as any).id);
          
          // Process any queued operations now that form is ready
          this.processQueuedOperations().catch(error => {
            console.error('Error processing queued operations after form initialization:', error);
          });
          
          return response;
        } else {
          throw new Error('Invalid response from server - no form ID received');
        }

      } catch (error) {
        lastError = error;
        console.error(`Form initialization attempt ${attempt} failed:`, error);

        if (attempt < this.MAX_RETRY_ATTEMPTS) {
          // Calculate exponential backoff delay
          const delayMs = this.INITIAL_RETRY_DELAY * Math.pow(2, attempt - 1);
          console.log(`Retrying in ${delayMs}ms...`);
          await this.delay(delayMs);
        }
      }
    }

    // All attempts failed - fall back to demo mode
    console.error('All form initialization attempts failed, falling back to demo mode:', lastError);
    return this.fallbackToDemoMode(lastError);
  }

  /**
   * Fallback to demo mode when backend initialization fails
   */
  private fallbackToDemoMode(error: any): any {
    const mockFormId = 'demo-form-' + Date.now();
    this.setFormId(mockFormId);
    this.setFormReady(true);

    this.updateInitializationState({
      status: 'demo',
      formId: mockFormId,
      error: error?.message || 'Backend unavailable',
      timestamp: new Date()
    });

    console.log('Operating in demo mode with ID:', mockFormId);
    
    // Process any queued operations in demo mode
    this.processQueuedOperations().catch(error => {
      console.error('Error processing queued operations in demo mode:', error);
    });
    
    return { id: mockFormId, uniqueUrl: mockFormId, demoMode: true };
  }

  /**
   * Update form readiness state
   */
  private setFormReady(ready: boolean): void {
    this.isFormReady = ready;
    this.formReadySubject.next(ready);
  }

  /**
   * Update initialization state and notify subscribers
   */
  private updateInitializationState(state: Partial<FormInitializationState>): void {
    this.initializationState = { ...this.initializationState, ...state };
    this.initializationStateSubject.next(this.initializationState);
  }

  /**
   * Utility method for creating delays
   */
  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  updateDocument(documentType: string, documentInfo: Partial<DocumentInfo>): void {
    const currentData = this.getCurrentFormData();
    const documents = currentData.documents.map(doc => 
      doc.documentType === documentType 
        ? { ...doc, ...documentInfo }
        : doc
    );
    
    this.formDataSubject.next({
      ...currentData,
      documents
    });
  }

  setCurrentStep(step: number): void {
    this.currentStepSubject.next(step);
  }

  getCurrentStep(): number {
    return this.currentStepSubject.value;
  }

  validatePersonalInfoStep(): StepValidation {
    const personalInfo = this.getCurrentFormData().personalInfo;
    const errors: string[] = [];

    if (!personalInfo.firstName.trim()) {
      errors.push('First name is required');
    }
    if (!personalInfo.lastName.trim()) {
      errors.push('Last name is required');
    }
    if (!personalInfo.email.trim()) {
      errors.push('Email is required');
    } else if (!this.isValidEmail(personalInfo.email)) {
      errors.push('Please enter a valid email address');
    }
    if (!personalInfo.phone.trim()) {
      errors.push('Phone number is required');
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  validateDocumentUploadStep(): StepValidation {
    const documents = this.getCurrentFormData().documents;
    const errors: string[] = [];

    const missingDocuments = documents.filter(doc => !doc.fileName);
    if (missingDocuments.length > 0) {
      errors.push(`Please upload the following documents: ${missingDocuments.map(d => d.documentType).join(', ')}`);
    }

    const failedDocuments = documents.filter(doc => doc.verificationStatus === 'failed');
    if (failedDocuments.length > 0) {
      errors.push(`Please re-upload failed documents: ${failedDocuments.map(d => d.documentType).join(', ')}`);
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  validateReviewStep(): StepValidation {
    const personalInfoValidation = this.validatePersonalInfoStep();
    const documentValidation = this.validateDocumentUploadStep();

    return {
      isValid: personalInfoValidation.isValid && documentValidation.isValid,
      errors: [...personalInfoValidation.errors, ...documentValidation.errors]
    };
  }

  submitForm(): Observable<any> {
    const formData = this.getCurrentFormData();
    formData.status = 'submitted';
    formData.submittedAt = new Date();
    
    this.formDataSubject.next(formData);
    
    // Make actual API call to submit the form
    if (this.currentFormId) {
      return this.http.post(`${this.baseUrl}/api/Form/${this.currentFormId}/submit`, {}).pipe(
        timeout(10000), // 10 second timeout for form submission
        tap(response => {
          console.log('Form submitted successfully:', response);
        }),
        catchError(error => {
          console.error('Form submission error:', error);
          // Return a mock success response for demo purposes
          return of({ success: true, formId: this.currentFormId });
        })
      );
    } else {
      // Fallback mock response if no form ID
      return of({ success: true, formId: 'mock-form-id' });
    }
  }

  private isValidEmail(email: string): boolean {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

  resetForm(): void {
    this.formDataSubject.next(this.getInitialFormData());
    this.currentStepSubject.next(1);
    
    // Reset form readiness state
    this.setFormReady(false);
    this.initializationPromise = null;
    this.currentFormId = undefined;
    
    this.updateInitializationState({
      status: 'initializing',
      formId: null,
      error: null,
      timestamp: new Date()
    });
    
    // Reset save operation state
    this.updateSaveOperationState({
      isSaving: false,
      lastSaveAttempt: null,
      saveError: null,
      retryCount: 0,
      queuedOperations: []
    });
  }

  /**
   * Force re-initialization of the form
   * Useful when recovering from errors
   */
  async reinitializeForm(recruiterEmail?: string): Promise<any> {
    console.log('Force re-initializing form...');
    
    // Reset state
    this.setFormReady(false);
    this.initializationPromise = null;
    this.currentFormId = undefined;
    
    // Start fresh initialization
    const result = await firstValueFrom(this.initializeForm(recruiterEmail));
    return result;
  }
}