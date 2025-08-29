import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { PersonalInfoStepComponent } from './personal-info-step.component';
import { FormService } from '../../services/form.service';
import { SecurityService } from '../../services/security.service';
import { of } from 'rxjs';

describe('PersonalInfoStepComponent', () => {
  let component: PersonalInfoStepComponent;
  let fixture: ComponentFixture<PersonalInfoStepComponent>;
  let formService: jasmine.SpyObj<FormService>;
  let securityService: jasmine.SpyObj<SecurityService>;

  beforeEach(async () => {
    const formServiceSpy = jasmine.createSpyObj('FormService', [
      'updatePersonalInfo', 'getCurrentFormData'
    ], {
      formData$: of({ personalInfo: null }),
      currentStep$: of(1)
    });
    
    const securityServiceSpy = jasmine.createSpyObj('SecurityService', [
      'sanitizeInput', 'validateFormData'
    ]);

    await TestBed.configureTestingModule({
      declarations: [ PersonalInfoStepComponent ],
      imports: [ 
        ReactiveFormsModule,
        HttpClientTestingModule
      ],
      providers: [
        FormBuilder,
        { provide: FormService, useValue: formServiceSpy },
        { provide: SecurityService, useValue: securityServiceSpy }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PersonalInfoStepComponent);
    component = fixture.componentInstance;
    formService = TestBed.inject(FormService) as jasmine.SpyObj<FormService>;
    securityService = TestBed.inject(SecurityService) as jasmine.SpyObj<SecurityService>;

    // Setup default return values
    formService.getCurrentFormData.and.returnValue({ personalInfo: null });
    securityService.sanitizeInput.and.returnValue('sanitized');
    securityService.validateFormData.and.returnValue({ isValid: true, errors: [] });

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize form with empty values', () => {
    expect(component.personalInfoForm).toBeDefined();
    expect(component.personalInfoForm.get('firstName')?.value).toBe('');
    expect(component.personalInfoForm.get('lastName')?.value).toBe('');
    expect(component.personalInfoForm.get('email')?.value).toBe('');
    expect(component.personalInfoForm.get('phone')?.value).toBe('');
    expect(component.personalInfoForm.get('address')?.value).toBe('');
    expect(component.personalInfoForm.get('dateOfBirth')?.value).toBe('');
  });

  it('should load form data from service on init', () => {
    const personalInfo = {
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      phone: '123-456-7890',
      address: '123 Main St',
      dateOfBirth: new Date('1990-01-01')
    };

    formService.getCurrentFormData.and.returnValue({ personalInfo });

    component.ngOnInit();

    expect(component.personalInfoForm.get('firstName')?.value).toBe('John');
    expect(component.personalInfoForm.get('lastName')?.value).toBe('Doe');
    expect(component.personalInfoForm.get('email')?.value).toBe('john@example.com');
  });

  it('should validate required fields', () => {
    const firstNameControl = component.personalInfoForm.get('firstName');
    firstNameControl?.setValue('');
    firstNameControl?.markAsTouched();

    expect(component.isFieldInvalid('firstName')).toBe(true);
  });

  it('should validate email format', () => {
    const emailControl = component.personalInfoForm.get('email');
    emailControl?.setValue('invalid-email');
    emailControl?.markAsTouched();

    expect(component.isFieldInvalid('email')).toBe(true);
  });

  it('should validate phone format', () => {
    const phoneControl = component.personalInfoForm.get('phone');
    phoneControl?.setValue('123');
    phoneControl?.markAsTouched();

    expect(component.isFieldInvalid('phone')).toBe(true);
  });

  it('should validate date of birth', () => {
    const dobControl = component.personalInfoForm.get('dateOfBirth');
    dobControl?.setValue('2030-01-01'); // Future date
    dobControl?.markAsTouched();

    expect(component.isFieldInvalid('dateOfBirth')).toBe(true);
  });

  it('should submit form when valid', () => {
    component.personalInfoForm.patchValue({
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      phone: '123-456-7890',
      address: '123 Main St',
      dateOfBirth: '1990-01-01'
    });

    component.onSubmit();

    expect(formService.updatePersonalInfo).toHaveBeenCalled();
  });

  it('should mark fields as touched when form is invalid on submit', () => {
    component.personalInfoForm.patchValue({
      firstName: '',
      lastName: '',
      email: 'invalid-email'
    });

    component.onSubmit();

    expect(component.personalInfoForm.get('firstName')?.touched).toBe(true);
    expect(component.personalInfoForm.get('lastName')?.touched).toBe(true);
    expect(component.personalInfoForm.get('email')?.touched).toBe(true);
  });

  it('should check if field is valid', () => {
    const firstNameControl = component.personalInfoForm.get('firstName');
    firstNameControl?.setValue('John');
    firstNameControl?.markAsTouched();

    expect(component.isFieldValid('firstName')).toBe(true);
  });

  it('should get field error message', () => {
    const firstNameControl = component.personalInfoForm.get('firstName');
    firstNameControl?.setValue('');
    firstNameControl?.markAsTouched();

    const error = component.getFieldError('firstName');
    expect(error).toBeTruthy();
  });

  it('should sanitize form data before submission', () => {
    component.personalInfoForm.patchValue({
      firstName: 'John<script>',
      lastName: 'Doe',
      email: 'john@example.com',
      phone: '123-456-7890',
      address: '123 Main St',
      dateOfBirth: '1990-01-01'
    });

    component.onSubmit();

    expect(securityService.sanitizeInput).toHaveBeenCalledWith('John<script>', { maxLength: 50 });
    expect(securityService.validateFormData).toHaveBeenCalled();
  });

  it('should handle security validation failure', () => {
    securityService.validateFormData.and.returnValue({ 
      isValid: false, 
      errors: ['Invalid input detected'] 
    });

    component.personalInfoForm.patchValue({
      firstName: 'John',
      lastName: 'Doe',
      email: 'john@example.com',
      phone: '123-456-7890',
      address: '123 Main St',
      dateOfBirth: '1990-01-01'
    });

    spyOn(console, 'error');
    component.onSubmit();

    expect(console.error).toHaveBeenCalledWith('Security validation failed:', ['Invalid input detected']);
    expect(formService.updatePersonalInfo).not.toHaveBeenCalled();
  });
});