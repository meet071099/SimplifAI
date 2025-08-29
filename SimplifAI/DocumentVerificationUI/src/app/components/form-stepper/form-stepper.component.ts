import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { FormService } from '../../services/form.service';
import { FormData } from '../../models/form.models';

@Component({
  selector: 'app-form-stepper',
  templateUrl: './form-stepper.component.html',
  styleUrls: ['./form-stepper.component.css']
})
export class FormStepperComponent implements OnInit, OnDestroy {
  currentStep = 1;
  totalSteps = 3;
  formId: string = '';
  formData: FormData | null = null;
  isTransitioning = false;
  
  // Form initialization management properties
  isInitializing = true;
  formReady = false;
  isDemoMode = false;
  initializationError: string | null = null;
  
  steps = [
    { id: 1, name: 'Personal Information', route: 'personal-info', completed: false, valid: false },
    { id: 2, name: 'Document Upload', route: 'document-upload', completed: false, valid: false },
    { id: 3, name: 'Review', route: 'review', completed: false, valid: false }
  ];

  private destroy$ = new Subject<void>();

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private formService: FormService
  ) {}

  ngOnInit(): void {
    // Subscribe to form initialization state
    this.formService.initializationState$.pipe(takeUntil(this.destroy$)).subscribe(state => {
      this.isInitializing = state.status === 'initializing';
      this.formReady = state.status === 'ready' || state.status === 'demo';
      this.isDemoMode = state.status === 'demo';
      this.initializationError = state.status === 'error' ? state.error : null;
      
      // Update form ID when available
      if (state.formId) {
        this.formId = state.formId;
      }
    });

    // Subscribe to form data changes
    this.formService.formData$.pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.formData = data;
      if (this.formReady) {
        this.updateStepValidation();
      }
    });

    // Subscribe to current step changes
    this.formService.currentStep$.pipe(takeUntil(this.destroy$)).subscribe(step => {
      this.currentStep = step;
    });

    // Initialize current step to 1
    this.currentStep = 1;
    this.formService.setCurrentStep(1);

    // Handle route parameters and form initialization
    this.route.params.pipe(takeUntil(this.destroy$)).subscribe(async params => {
      const routeFormId = params['id'];
      
      try {
        if (routeFormId && routeFormId !== 'new' && routeFormId !== 'current') {
          // Existing form - set ID and mark as ready
          this.formService.setFormId(routeFormId);
          this.formId = routeFormId;
          this.isInitializing = false;
          this.formReady = true;
        } else {
          // New form - wait for initialization to complete
          await this.initializeNewFormAndWait();
        }
      } catch (error) {
        this.handleInitializationError(error);
      }
    });
  }

  /**
   * Initialize a new form and wait for completion
   */
  private async initializeNewFormAndWait(): Promise<void> {
    try {
      this.isInitializing = true;
      this.initializationError = null;
      
      // Start form initialization
      this.formService.initializeForm().subscribe();
      
      // Wait for form to be ready
      await this.formService.waitForFormReady();
      
      // Get the final form ID and update URL
      const finalFormId = this.formService.getFormId();
      if (finalFormId) {
        this.formId = finalFormId;
        await this.updateUrlAfterInitialization(finalFormId);
      }
      
      console.log('Form initialization completed successfully');
      
    } catch (error) {
      console.error('Form initialization failed:', error);
      this.handleInitializationError(error);
    }
  }

  /**
   * Update URL after successful form creation
   */
  private async updateUrlAfterInitialization(formId: string): Promise<void> {
    try {
      // Only update URL if we got a real form ID from the backend (not demo mode)
      if (formId && !formId.startsWith('demo-form-')) {
        await this.router.navigate(['/form', formId], { replaceUrl: true });
        console.log('URL updated to use real form ID:', formId);
      } else if (formId && formId.startsWith('demo-form-')) {
        // In demo mode, keep the URL as /form/new but update internal state
        console.log('Operating in demo mode, keeping URL as /form/new');
      }
    } catch (error) {
      console.error('Error updating URL after form initialization:', error);
      // Non-critical error, don't fail the entire initialization
    }
  }

  /**
   * Handle initialization failures with user-friendly messages
   */
  private handleInitializationError(error: any): void {
    console.error('Form initialization error:', error);
    
    let errorMessage = 'Unable to initialize form. ';
    
    if (error && typeof error === 'object') {
      if ('status' in error) {
        switch (error.status) {
          case 0:
            errorMessage += 'Please check your internet connection and try again.';
            break;
          case 404:
            errorMessage += 'Form service not found. Please contact support.';
            break;
          case 500:
            errorMessage += 'Server error occurred. Please try again later.';
            break;
          default:
            errorMessage += 'An unexpected error occurred. Please try again.';
        }
      } else if (error.message) {
        errorMessage += error.message;
      } else {
        errorMessage += 'An unexpected error occurred. Please try again.';
      }
    } else {
      errorMessage += 'An unexpected error occurred. Please try again.';
    }
    
    this.initializationError = errorMessage;
    this.isInitializing = false;
    this.formReady = false;
  }

  /**
   * Retry form initialization
   */
  async retryInitialization(): Promise<void> {
    this.initializationError = null;
    try {
      await this.formService.reinitializeForm();
      console.log('Form re-initialization completed');
    } catch (error) {
      this.handleInitializationError(error);
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }



  private updateStepValidation(): void {
    // Validate each step and update completion status
    const personalInfoValidation = this.formService.validatePersonalInfoStep();
    const documentValidation = this.formService.validateDocumentUploadStep();
    const reviewValidation = this.formService.validateReviewStep();

    this.steps[0].valid = personalInfoValidation.isValid;
    this.steps[1].valid = documentValidation.isValid;
    this.steps[2].valid = reviewValidation.isValid;

    // Mark steps as completed if they are valid and we've moved past them
    this.steps[0].completed = personalInfoValidation.isValid && this.currentStep > 1;
    this.steps[1].completed = documentValidation.isValid && this.currentStep > 2;
    this.steps[2].completed = reviewValidation.isValid && this.formData?.status === 'submitted';
  }

  navigateToStep(stepId: number): void {
    if (this.canNavigateToStep(stepId)) {
      this.isTransitioning = true;
      setTimeout(() => {
        this.currentStep = stepId;
        this.formService.setCurrentStep(stepId);
        setTimeout(() => {
          this.isTransitioning = false;
        }, 400);
      }, 100);
    }
  }

  canNavigateToStep(stepId: number): boolean {
    // Don't allow navigation if form is not ready
    if (!this.formReady || this.isInitializing) {
      return false;
    }
    
    // Always allow navigation to current step or previous steps
    if (stepId <= this.currentStep) {
      return true;
    }
    
    // Allow navigation to next step only if current step is valid
    if (stepId === this.currentStep + 1) {
      return this.steps[this.currentStep - 1].valid;
    }
    
    return false;
  }

  nextStep(): void {
    if (this.currentStep < this.totalSteps && this.canNavigateToStep(this.currentStep + 1)) {
      this.navigateToStep(this.currentStep + 1);
    }
  }

  previousStep(): void {
    if (this.currentStep > 1) {
      this.navigateToStep(this.currentStep - 1);
    }
  }

  getProgressPercentage(): number {
    return (this.currentStep / this.totalSteps) * 100;
  }

  isNextStepDisabled(): boolean {
    return !this.formReady || this.isInitializing || this.currentStep >= this.totalSteps || !this.steps[this.currentStep - 1].valid;
  }

  isPreviousStepDisabled(): boolean {
    return !this.formReady || this.isInitializing || this.currentStep <= 1;
  }
}