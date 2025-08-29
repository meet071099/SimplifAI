import { Component, OnInit, OnDestroy, Input } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil, debounceTime, combineLatest } from 'rxjs';
import { FormService } from '../../services/form.service';
import { PersonalInfo } from '../../models/form.models';
import { FormValidationService } from '../../services/form-validation.service';
import { SecurityService } from '../../services/security.service';

@Component({
  selector: 'app-personal-info-step',
  templateUrl: './personal-info-step.component.html',
  styleUrls: ['./personal-info-step.component.css']
})
export class PersonalInfoStepComponent implements OnInit, OnDestroy {
  @Input() formId?: string;
  personalInfoForm: FormGroup;
  private destroy$ = new Subject<void>();

  // Form readiness state
  isFormReady = false;
  isInitializing = true;
  initializationError: string | null = null;
  isDemoMode = false;

  // Save operation state
  isSaving = false;
  saveError: string | null = null;
  lastSaveAttempt: Date | null = null;

  constructor(
    private fb: FormBuilder,
    private formService: FormService,
    private securityService: SecurityService
  ) {
    this.personalInfoForm = this.createForm();
  }

  ngOnInit(): void {
    // Subscribe to form readiness and initialization state
    this.subscribeToFormReadiness();

    // Subscribe to save operation state for visual feedback
    this.subscribeToSaveOperationState();

    // Load existing form data
    this.loadFormData();

    // Subscribe to form changes with form readiness check
    this.setupFormValueChangeSubscription();

    // Subscribe to form data changes from service
    this.subscribeToFormDataChanges();
  }

  ngOnDestroy(): void {
    // Save current form state before destroying
    if (this.personalInfoForm.valid) {
      this.updateFormService(this.personalInfoForm.value);
    }
    
    this.destroy$.next();
    this.destroy$.complete();
  }

  private createForm(): FormGroup {
    return this.fb.group({
      firstName: ['', [Validators.required, FormValidationService.nameValidator()]],
      lastName: ['', [Validators.required, FormValidationService.nameValidator()]],
      email: ['', [Validators.required, FormValidationService.emailValidator()]],
      phone: ['', [Validators.required, FormValidationService.phoneValidator()]],
      address: ['', [Validators.required, FormValidationService.addressValidator()]],
      dateOfBirth: ['', [Validators.required, FormValidationService.dateOfBirthValidator()]]
    });
  }

  private dateOfBirthValidator(control: any) {
    if (!control.value) return null;
    
    const selectedDate = new Date(control.value);
    const today = new Date();
    const minAge = 16;
    const maxAge = 100;
    
    const age = today.getFullYear() - selectedDate.getFullYear();
    const monthDiff = today.getMonth() - selectedDate.getMonth();
    
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < selectedDate.getDate())) {
      // Adjust age if birthday hasn't occurred this year
    }
    
    if (selectedDate > today) {
      return { futureDate: true };
    }
    
    if (age < minAge) {
      return { tooYoung: true };
    }
    
    if (age > maxAge) {
      return { tooOld: true };
    }
    
    return null;
  }

  private loadFormData(): void {
    const currentData = this.formService.getCurrentFormData();
    if (currentData.personalInfo) {
      const formData = { ...currentData.personalInfo };
      
      // Format date for HTML date input (YYYY-MM-DD)
      const formattedData = {
        ...formData,
        dateOfBirth: formData.dateOfBirth ? this.formatDateForInput(formData.dateOfBirth) : null
      };
      
      this.personalInfoForm.patchValue(formattedData);
    }
  }

  private formatDateForInput(date: Date | string | null): string | null {
    if (!date) {
      return null;
    }

    let dateObj: Date;
    
    if (date instanceof Date) {
      dateObj = date;
    } else if (typeof date === 'string') {
      dateObj = new Date(date);
    } else {
      return null;
    }

    // Check if date is valid
    if (isNaN(dateObj.getTime())) {
      return null;
    }

    // Format as YYYY-MM-DD for HTML date input
    const year = dateObj.getFullYear();
    const month = String(dateObj.getMonth() + 1).padStart(2, '0');
    const day = String(dateObj.getDate()).padStart(2, '0');
    
    return `${year}-${month}-${day}`;
  }

  private updateFormService(value: any): void {
    const personalInfo: PersonalInfo = {
      firstName: value.firstName || '',
      lastName: value.lastName || '',
      email: value.email || '',
      phone: value.phone || '',
      address: value.address || '',
      dateOfBirth: this.parseDateOfBirth(value.dateOfBirth)
    };
    
    this.formService.updatePersonalInfo(personalInfo);
  }

  private parseDateOfBirth(dateValue: any): Date | null {
    if (!dateValue) {
      return null;
    }

    // If it's already a Date object, return it
    if (dateValue instanceof Date) {
      return dateValue;
    }

    // If it's a string, try to parse it
    if (typeof dateValue === 'string') {
      // Handle HTML date input format (YYYY-MM-DD)
      const dateString = dateValue.trim();
      if (dateString === '') {
        return null;
      }

      // Create date from string, ensuring it's treated as local date
      const parsedDate = new Date(dateString + 'T00:00:00');
      
      // Check if the date is valid
      if (isNaN(parsedDate.getTime())) {
        console.warn('Invalid date value:', dateValue);
        return null;
      }

      return parsedDate;
    }

    console.warn('Unexpected date value type:', typeof dateValue, dateValue);
    return null;
  }

  getFieldError(fieldName: string): string {
    const field = this.personalInfoForm.get(fieldName);
    if (!field) return '';
    
    return FormValidationService.getErrorMessage(field, this.getFieldDisplayName(fieldName));
  }

  private getFieldDisplayName(fieldName: string): string {
    const displayNames: { [key: string]: string } = {
      firstName: 'First name',
      lastName: 'Last name',
      email: 'Email',
      phone: 'Phone number',
      address: 'Address',
      dateOfBirth: 'Date of birth'
    };
    return displayNames[fieldName] || fieldName;
  }

  isFieldInvalid(fieldName: string): boolean {
    const field = this.personalInfoForm.get(fieldName);
    return !!(field && field.errors && field.touched);
  }

  isFieldValid(fieldName: string): boolean {
    const field = this.personalInfoForm.get(fieldName);
    return !!(field && field.valid && field.touched);
  }

  onSubmit(): void {
    if (this.personalInfoForm.valid) {
      // Sanitize form data before submission
      const formData = this.sanitizeFormData(this.personalInfoForm.value);
      
      // Validate sanitized data for security
      const securityValidation = this.securityService.validateFormData(formData);
      if (!securityValidation.isValid) {
        console.error('Security validation failed:', securityValidation.errors);
        return;
      }
      
      this.updateFormService(formData);
    } else {
      // Mark all fields as touched to show validation errors
      Object.keys(this.personalInfoForm.controls).forEach(key => {
        this.personalInfoForm.get(key)?.markAsTouched();
      });
    }
  }

  private sanitizeFormData(formData: any): PersonalInfo {
    return {
      firstName: this.securityService.sanitizeInput(formData.firstName, { maxLength: 50 }),
      lastName: this.securityService.sanitizeInput(formData.lastName, { maxLength: 50 }),
      email: this.securityService.sanitizeInput(formData.email, { maxLength: 254 }),
      phone: this.securityService.sanitizeInput(formData.phone, { maxLength: 20 }),
      address: this.securityService.sanitizeInput(formData.address, { maxLength: 200 }),
      dateOfBirth: formData.dateOfBirth
    };
  }

  /**
   * Subscribe to form readiness and initialization state
   */
  private subscribeToFormReadiness(): void {
    // Subscribe to form ready state
    this.formService.formReady$.pipe(takeUntil(this.destroy$)).subscribe(ready => {
      this.isFormReady = ready;
      this.isInitializing = !ready;
      
      // Enable/disable form based on readiness
      if (ready) {
        this.personalInfoForm.enable();
      } else {
        this.personalInfoForm.disable();
      }
    });

    // Subscribe to initialization state for detailed feedback
    this.formService.initializationState$.pipe(takeUntil(this.destroy$)).subscribe(state => {
      this.isInitializing = state.status === 'initializing';
      this.initializationError = state.error;
      this.isDemoMode = state.status === 'demo';
      
      // Update form state based on initialization status
      if (state.status === 'ready' || state.status === 'demo') {
        this.personalInfoForm.enable();
      } else {
        this.personalInfoForm.disable();
      }
    });
  }

  /**
   * Subscribe to save operation state for visual feedback
   */
  private subscribeToSaveOperationState(): void {
    this.formService.saveOperationState$.pipe(takeUntil(this.destroy$)).subscribe(state => {
      this.isSaving = state.isSaving;
      this.saveError = state.saveError;
      this.lastSaveAttempt = state.lastSaveAttempt;
    });
  }

  /**
   * Setup form value change subscription with form readiness check
   */
  private setupFormValueChangeSubscription(): void {
    // Combine form value changes with form readiness state
    combineLatest([
      this.personalInfoForm.valueChanges.pipe(debounceTime(300)),
      this.formService.formReady$
    ]).pipe(
      takeUntil(this.destroy$)
    ).subscribe(([value, isReady]) => {
      // Only attempt to save if form is ready and valid
      if (isReady && this.personalInfoForm.valid) {
        this.updateFormService(value);
      } else if (!isReady && this.personalInfoForm.valid) {
        // Form is valid but not ready - this will be queued by the service
        console.log('Form not ready - personal info changes will be queued');
        this.updateFormService(value);
      }
    });
  }

  /**
   * Subscribe to form data changes from service
   */
  private subscribeToFormDataChanges(): void {
    this.formService.formData$.pipe(takeUntil(this.destroy$)).subscribe(data => {
      if (data.personalInfo) {
        const formData = { ...data.personalInfo };
        
        // Format date for HTML date input
        const formattedData = {
          ...formData,
          dateOfBirth: formData.dateOfBirth ? this.formatDateForInput(formData.dateOfBirth) : null
        };
        
        this.personalInfoForm.patchValue(formattedData, { emitEvent: false });
      }
    });
  }

  /**
   * Get user-friendly status message based on current state
   */
  getStatusMessage(): string {
    if (this.isInitializing) {
      return 'Initializing form...';
    }
    
    if (this.initializationError && !this.isDemoMode) {
      return 'Form initialization failed. Please try again.';
    }
    
    if (this.isDemoMode) {
      return 'Operating in demo mode - changes will not be saved to server';
    }
    
    if (this.isSaving) {
      return 'Saving your information...';
    }
    
    if (this.saveError) {
      return `Save failed: ${this.saveError}`;
    }
    
    if (this.lastSaveAttempt && this.isFormReady) {
      return 'Information saved successfully';
    }
    
    return 'Ready for input';
  }

  /**
   * Get CSS class for status message styling
   */
  getStatusMessageClass(): string {
    if (this.isInitializing) {
      return 'status-initializing';
    }
    
    if (this.initializationError && !this.isDemoMode) {
      return 'status-error';
    }
    
    if (this.isDemoMode) {
      return 'status-demo';
    }
    
    if (this.isSaving) {
      return 'status-saving';
    }
    
    if (this.saveError) {
      return 'status-error';
    }
    
    if (this.lastSaveAttempt && this.isFormReady) {
      return 'status-success';
    }
    
    return 'status-ready';
  }

  /**
   * Check if form interactions should be disabled
   */
  isFormDisabled(): boolean {
    return !this.isFormReady || this.isInitializing;
  }
}