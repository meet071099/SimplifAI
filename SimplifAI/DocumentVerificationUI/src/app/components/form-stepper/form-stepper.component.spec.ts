import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute, Router, NavigationEnd } from '@angular/router';
import { of, Subject } from 'rxjs';

import { FormStepperComponent } from './form-stepper.component';
import { FormService } from '../../services/form.service';
import { FormData } from '../../models/form.models';

describe('FormStepperComponent', () => {
  let component: FormStepperComponent;
  let fixture: ComponentFixture<FormStepperComponent>;
  let formService: jasmine.SpyObj<FormService>;
  let router: jasmine.SpyObj<Router>;
  let routerEventsSubject: Subject<any>;

  beforeEach(async () => {
    routerEventsSubject = new Subject();
    
    const formServiceSpy = jasmine.createSpyObj('FormService', [
      'setCurrentStep', 'validatePersonalInfoStep', 'validateDocumentUploadStep', 'validateReviewStep'
    ], {
      formData$: of(null),
      currentStep$: of(1)
    });
    
    const routerSpy = jasmine.createSpyObj('Router', ['navigate'], {
      events: routerEventsSubject.asObservable(),
      url: '/form/test-form-id/personal-info'
    });

    await TestBed.configureTestingModule({
      declarations: [ FormStepperComponent ],
      imports: [ 
        HttpClientTestingModule,
        RouterTestingModule
      ],
      providers: [
        { provide: FormService, useValue: formServiceSpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({ id: 'test-form-id' })
          }
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FormStepperComponent);
    component = fixture.componentInstance;
    formService = TestBed.inject(FormService) as jasmine.SpyObj<FormService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default values', () => {
    expect(component.currentStep).toBe(1);
    expect(component.totalSteps).toBe(3);
    expect(component.isTransitioning).toBe(false);
    expect(component.formId).toBe('');
    expect(component.formData).toBeNull();
  });

  it('should initialize form ID from route params', () => {
    formService.validatePersonalInfoStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateDocumentUploadStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateReviewStep.and.returnValue({ isValid: false, errors: [] });

    component.ngOnInit();

    expect(component.formId).toBe('test-form-id');
  });

  it('should update current step based on URL', () => {
    formService.validatePersonalInfoStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateDocumentUploadStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateReviewStep.and.returnValue({ isValid: false, errors: [] });

    // Test personal-info route
    (router as any).url = '/form/test-form-id/personal-info';
    component.ngOnInit();
    expect(component.currentStep).toBe(1);

    // Test document-upload route
    (router as any).url = '/form/test-form-id/document-upload';
    component['updateCurrentStep']();
    expect(component.currentStep).toBe(2);

    // Test review route
    (router as any).url = '/form/test-form-id/review';
    component['updateCurrentStep']();
    expect(component.currentStep).toBe(3);
  });

  it('should navigate to next step when valid', () => {
    component.currentStep = 1;
    component.steps[0].valid = true;
    spyOn(component, 'canNavigateToStep').and.returnValue(true);

    component.nextStep();

    expect(router.navigate).toHaveBeenCalledWith(['/form', 'test-form-id', 'document-upload']);
  });

  it('should navigate to previous step', () => {
    component.currentStep = 2;

    component.previousStep();

    expect(router.navigate).toHaveBeenCalledWith(['/form', 'test-form-id', 'personal-info']);
  });

  it('should not navigate below step 1', () => {
    component.currentStep = 1;
    const navigateSpy = router.navigate;

    component.previousStep();

    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('should navigate to specific step when valid', () => {
    spyOn(component, 'canNavigateToStep').and.returnValue(true);

    component.navigateToStep(3);

    expect(component.isTransitioning).toBe(true);
  });

  it('should not navigate to specific step when invalid', () => {
    spyOn(component, 'canNavigateToStep').and.returnValue(false);
    const navigateSpy = router.navigate;

    component.navigateToStep(3);

    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('should validate step navigation correctly', () => {
    // Can always navigate to current or previous steps
    component.currentStep = 2;
    expect(component.canNavigateToStep(1)).toBe(true);
    expect(component.canNavigateToStep(2)).toBe(true);

    // Can navigate to next step only if current step is valid
    component.steps[1].valid = true;
    expect(component.canNavigateToStep(3)).toBe(true);

    component.steps[1].valid = false;
    expect(component.canNavigateToStep(3)).toBe(false);
  });

  it('should calculate progress percentage correctly', () => {
    component.currentStep = 1;
    expect(component.getProgressPercentage()).toBe(33.333333333333336);

    component.currentStep = 2;
    expect(component.getProgressPercentage()).toBe(66.66666666666667);

    component.currentStep = 3;
    expect(component.getProgressPercentage()).toBe(100);
  });

  it('should determine next step disabled state', () => {
    component.currentStep = 1;
    component.steps[0].valid = false;
    expect(component.isNextStepDisabled()).toBe(true);

    component.steps[0].valid = true;
    expect(component.isNextStepDisabled()).toBe(false);

    component.currentStep = 3;
    expect(component.isNextStepDisabled()).toBe(true);
  });

  it('should determine previous step disabled state', () => {
    component.currentStep = 1;
    expect(component.isPreviousStepDisabled()).toBe(true);

    component.currentStep = 2;
    expect(component.isPreviousStepDisabled()).toBe(false);
  });

  it('should update step validation when form data changes', () => {
    const mockFormData: FormData = {
      id: 'test-form-id',
      personalInfo: {
        firstName: 'John',
        lastName: 'Doe',
        email: 'john@example.com',
        phone: '123-456-7890',
        address: '123 Main St',
        dateOfBirth: new Date('1990-01-01')
      },
      documents: [],
      status: 'pending',
      createdAt: new Date(),
      submittedAt: null
    };

    formService.validatePersonalInfoStep.and.returnValue({ isValid: true, errors: [] });
    formService.validateDocumentUploadStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateReviewStep.and.returnValue({ isValid: false, errors: [] });

    // Simulate form data change
    (formService as any).formData$ = of(mockFormData);
    
    component.ngOnInit();

    expect(component.steps[0].valid).toBe(true);
    expect(component.steps[1].valid).toBe(false);
    expect(component.steps[2].valid).toBe(false);
  });

  it('should handle router navigation events', () => {
    formService.validatePersonalInfoStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateDocumentUploadStep.and.returnValue({ isValid: false, errors: [] });
    formService.validateReviewStep.and.returnValue({ isValid: false, errors: [] });

    component.ngOnInit();

    // Simulate navigation event
    (router as any).url = '/form/test-form-id/document-upload';
    routerEventsSubject.next(new NavigationEnd(1, '/form/test-form-id/document-upload', '/form/test-form-id/document-upload'));

    expect(component.currentStep).toBe(2);
  });
});