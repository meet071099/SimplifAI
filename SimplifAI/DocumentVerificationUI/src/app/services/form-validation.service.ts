import { Injectable } from '@angular/core';
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { SecurityService } from './security.service';

export interface ValidationResult {
  isValid: boolean;
  errors: ValidationError[];
}

export interface ValidationError {
  field: string;
  message: string;
  code: string;
}

@Injectable({
  providedIn: 'root'
})
export class FormValidationService {

  constructor(private securityService: SecurityService) {}

  /**
   * Enhanced email validator with detailed error messages
   */
  static emailValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null; // Let required validator handle empty values
      }

      const email = control.value.toString().trim();
      
      // Security validation first
      const securityValidation = new SecurityService().validateInput(email);
      if (!securityValidation.isValid) {
        return { 
          email: { 
            message: 'Email contains invalid characters',
            code: 'SECURITY_VALIDATION_FAILED'
          } 
        };
      }
      
      // Basic format check
      const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
      if (!emailRegex.test(email)) {
        return { 
          email: { 
            message: 'Please enter a valid email address (e.g., user@example.com)',
            code: 'INVALID_EMAIL_FORMAT'
          } 
        };
      }

      // Check for common issues
      if (email.includes('..')) {
        return { 
          email: { 
            message: 'Email address cannot contain consecutive dots',
            code: 'INVALID_EMAIL_CONSECUTIVE_DOTS'
          } 
        };
      }

      if (email.startsWith('.') || email.endsWith('.')) {
        return { 
          email: { 
            message: 'Email address cannot start or end with a dot',
            code: 'INVALID_EMAIL_DOT_POSITION'
          } 
        };
      }

      // Check length limits
      if (email.length > 254) {
        return { 
          email: { 
            message: 'Email address is too long (maximum 254 characters)',
            code: 'EMAIL_TOO_LONG'
          } 
        };
      }

      return null;
    };
  }

  /**
   * Enhanced phone number validator
   */
  static phoneValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const phone = control.value.toString().trim();
      
      // Remove common formatting characters
      const cleanPhone = phone.replace(/[\s\-\(\)\+]/g, '');
      
      // Check if it contains only digits after cleaning
      if (!/^\d+$/.test(cleanPhone)) {
        return { 
          phone: { 
            message: 'Phone number can only contain digits, spaces, hyphens, parentheses, and plus sign',
            code: 'INVALID_PHONE_CHARACTERS'
          } 
        };
      }

      // Check length (international format: 7-15 digits)
      if (cleanPhone.length < 7) {
        return { 
          phone: { 
            message: 'Phone number is too short (minimum 7 digits)',
            code: 'PHONE_TOO_SHORT'
          } 
        };
      }

      if (cleanPhone.length > 15) {
        return { 
          phone: { 
            message: 'Phone number is too long (maximum 15 digits)',
            code: 'PHONE_TOO_LONG'
          } 
        };
      }

      return null;
    };
  }

  /**
   * Enhanced date of birth validator
   */
  static dateOfBirthValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const selectedDate = new Date(control.value);
      const today = new Date();
      
      // Check if date is valid
      if (isNaN(selectedDate.getTime())) {
        return { 
          dateOfBirth: { 
            message: 'Please enter a valid date',
            code: 'INVALID_DATE'
          } 
        };
      }

      // Check if date is in the future
      if (selectedDate > today) {
        return { 
          dateOfBirth: { 
            message: 'Date of birth cannot be in the future',
            code: 'FUTURE_DATE'
          } 
        };
      }

      // Calculate age
      const age = today.getFullYear() - selectedDate.getFullYear();
      const monthDiff = today.getMonth() - selectedDate.getMonth();
      const dayDiff = today.getDate() - selectedDate.getDate();
      
      let actualAge = age;
      if (monthDiff < 0 || (monthDiff === 0 && dayDiff < 0)) {
        actualAge--;
      }

      // Check minimum age (16 years)
      if (actualAge < 16) {
        return { 
          dateOfBirth: { 
            message: 'You must be at least 16 years old',
            code: 'TOO_YOUNG'
          } 
        };
      }

      // Check maximum reasonable age (120 years)
      if (actualAge > 120) {
        return { 
          dateOfBirth: { 
            message: 'Please enter a valid date of birth',
            code: 'TOO_OLD'
          } 
        };
      }

      // Check if date is too far in the past (before 1900)
      if (selectedDate.getFullYear() < 1900) {
        return { 
          dateOfBirth: { 
            message: 'Please enter a date after 1900',
            code: 'DATE_TOO_OLD'
          } 
        };
      }

      return null;
    };
  }

  /**
   * Name validator (first name, last name)
   */
  static nameValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const name = control.value.toString().trim();
      
      // Check minimum length
      if (name.length < 2) {
        return { 
          name: { 
            message: 'Name must be at least 2 characters long',
            code: 'NAME_TOO_SHORT'
          } 
        };
      }

      // Check maximum length
      if (name.length > 50) {
        return { 
          name: { 
            message: 'Name must not exceed 50 characters',
            code: 'NAME_TOO_LONG'
          } 
        };
      }

      // Check for valid characters (letters, spaces, hyphens, apostrophes)
      const nameRegex = /^[a-zA-Z\s\-']+$/;
      if (!nameRegex.test(name)) {
        return { 
          name: { 
            message: 'Name can only contain letters, spaces, hyphens, and apostrophes',
            code: 'INVALID_NAME_CHARACTERS'
          } 
        };
      }

      // Check for consecutive spaces or special characters
      if (/[\s\-']{2,}/.test(name)) {
        return { 
          name: { 
            message: 'Name cannot contain consecutive spaces or special characters',
            code: 'INVALID_NAME_FORMAT'
          } 
        };
      }

      return null;
    };
  }

  /**
   * Address validator
   */
  static addressValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const address = control.value.toString().trim();
      
      // Check minimum length
      if (address.length < 10) {
        return { 
          address: { 
            message: 'Address must be at least 10 characters long',
            code: 'ADDRESS_TOO_SHORT'
          } 
        };
      }

      // Check maximum length
      if (address.length > 200) {
        return { 
          address: { 
            message: 'Address must not exceed 200 characters',
            code: 'ADDRESS_TOO_LONG'
          } 
        };
      }

      // Check for valid characters (letters, numbers, spaces, common punctuation)
      const addressRegex = /^[a-zA-Z0-9\s\-\.,#\/]+$/;
      if (!addressRegex.test(address)) {
        return { 
          address: { 
            message: 'Address contains invalid characters',
            code: 'INVALID_ADDRESS_CHARACTERS'
          } 
        };
      }

      return null;
    };
  }

  /**
   * File validation for document uploads
   */
  static validateFile(file: File): ValidationResult {
    const errors: ValidationError[] = [];
    
    // Check file size (10MB limit)
    const maxSize = 10 * 1024 * 1024;
    if (file.size > maxSize) {
      errors.push({
        field: 'file',
        message: `File size must be less than ${maxSize / (1024 * 1024)}MB. Current size: ${(file.size / (1024 * 1024)).toFixed(2)}MB`,
        code: 'FILE_TOO_LARGE'
      });
    }

    // Check minimum file size (1KB)
    if (file.size < 1024) {
      errors.push({
        field: 'file',
        message: 'File is too small. Please ensure the file is not corrupted.',
        code: 'FILE_TOO_SMALL'
      });
    }

    // Check file type
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'application/pdf'];
    if (!allowedTypes.includes(file.type.toLowerCase())) {
      errors.push({
        field: 'file',
        message: `File type '${file.type}' is not supported. Allowed types: JPEG, PNG, PDF`,
        code: 'INVALID_FILE_TYPE'
      });
    }

    // Check file extension
    const extension = '.' + file.name.split('.').pop()?.toLowerCase();
    const allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];
    if (!allowedExtensions.includes(extension)) {
      errors.push({
        field: 'file',
        message: `File extension '${extension}' is not allowed. Allowed extensions: ${allowedExtensions.join(', ')}`,
        code: 'INVALID_FILE_EXTENSION'
      });
    }

    // Check filename
    if (!file.name || file.name.trim().length === 0) {
      errors.push({
        field: 'file',
        message: 'File must have a valid name',
        code: 'INVALID_FILENAME'
      });
    }

    // Check for potentially dangerous filenames
    const dangerousPatterns = [/\.exe$/i, /\.bat$/i, /\.cmd$/i, /\.scr$/i, /\.com$/i];
    if (dangerousPatterns.some(pattern => pattern.test(file.name))) {
      errors.push({
        field: 'file',
        message: 'File type is not allowed for security reasons',
        code: 'DANGEROUS_FILE_TYPE'
      });
    }

    return {
      isValid: errors.length === 0,
      errors
    };
  }

  /**
   * Get user-friendly error message from validation errors
   */
  static getErrorMessage(control: AbstractControl, fieldName: string): string {
    if (!control.errors || !control.touched) {
      return '';
    }

    const errors = control.errors;
    
    // Handle custom validation errors with detailed messages
    for (const [errorType, errorValue] of Object.entries(errors)) {
      if (errorValue && typeof errorValue === 'object' && 'message' in errorValue) {
        return errorValue.message;
      }
    }

    // Handle standard Angular validators
    if (errors['required']) {
      return `${fieldName} is required`;
    }

    if (errors['minlength']) {
      return `${fieldName} must be at least ${errors['minlength'].requiredLength} characters`;
    }

    if (errors['maxlength']) {
      return `${fieldName} must not exceed ${errors['maxlength'].requiredLength} characters`;
    }

    if (errors['email']) {
      return 'Please enter a valid email address';
    }

    if (errors['pattern']) {
      return `${fieldName} format is invalid`;
    }

    // Default error message
    return `${fieldName} is invalid`;
  }

  /**
   * Validate entire form and return detailed results
   */
  static validateForm(form: any): ValidationResult {
    const errors: ValidationError[] = [];
    
    // Mark all fields as touched to trigger validation
    Object.keys(form.controls).forEach(key => {
      const control = form.get(key);
      if (control) {
        control.markAsTouched();
        
        if (control.errors) {
          const errorMessage = this.getErrorMessage(control, key);
          if (errorMessage) {
            errors.push({
              field: key,
              message: errorMessage,
              code: Object.keys(control.errors)[0] || 'VALIDATION_ERROR'
            });
          }
        }
      }
    });

    return {
      isValid: errors.length === 0,
      errors
    };
  }
}