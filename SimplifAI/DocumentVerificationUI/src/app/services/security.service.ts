import { Injectable } from '@angular/core';
import { HttpRequest } from '@angular/common/http';

export interface SecurityValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface InputSanitizationOptions {
  maxLength?: number;
  allowHtml?: boolean;
  allowSpecialChars?: boolean;
  trimWhitespace?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class SecurityService {

  private readonly suspiciousPatterns = [
    // XSS patterns
    /<script[^>]*>.*?<\/script>/gi,
    /javascript:/gi,
    /vbscript:/gi,
    /on\w+\s*=/gi,
    /<iframe[^>]*>/gi,
    /<object[^>]*>/gi,
    /<embed[^>]*>/gi,
    
    // SQL injection patterns
    /(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|UNION)\b)/gi,
    /(\b(OR|AND)\s+\d+\s*=\s*\d+)/gi,
    /(--|#|\/\*|\*\/)/g,
    
    // Directory traversal
    /\.\.\//g,
    /\.\.\\/g,
    
    // Command injection
    /(\||&|;|\$\(|`)/g
  ];

  private readonly dangerousFileExtensions = [
    '.exe', '.bat', '.cmd', '.scr', '.com', '.pif', '.vbs', '.js', '.jar',
    '.app', '.deb', '.pkg', '.dmg', '.msi', '.run', '.sh', '.ps1', '.php',
    '.asp', '.aspx', '.jsp', '.py', '.rb', '.pl', '.cgi'
  ];

  constructor() {}

  /**
   * Sanitizes user input to prevent XSS and injection attacks
   */
  sanitizeInput(input: string, options: InputSanitizationOptions = {}): string {
    if (!input) return '';

    const {
      maxLength = 1000,
      allowHtml = false,
      allowSpecialChars = true,
      trimWhitespace = true
    } = options;

    let sanitized = input;

    // Trim whitespace if requested
    if (trimWhitespace) {
      sanitized = sanitized.trim();
    }

    // Remove HTML tags if not allowed
    if (!allowHtml) {
      sanitized = sanitized.replace(/<[^>]*>/g, '');
    }

    // Remove script content
    sanitized = sanitized.replace(/<script[^>]*>.*?<\/script>/gi, '');

    // Remove dangerous JavaScript events
    sanitized = sanitized.replace(/on\w+\s*=/gi, '');

    // Remove suspicious patterns
    this.suspiciousPatterns.forEach(pattern => {
      sanitized = sanitized.replace(pattern, '');
    });

    // Remove null bytes and control characters
    sanitized = sanitized.replace(/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/g, '');

    // Remove special characters if not allowed
    if (!allowSpecialChars) {
      sanitized = sanitized.replace(/[<>'"&]/g, '');
    }

    // Truncate to max length
    if (sanitized.length > maxLength) {
      sanitized = sanitized.substring(0, maxLength);
    }

    return sanitized;
  }

  /**
   * Validates input for security threats
   */
  validateInput(input: string): SecurityValidationResult {
    const result: SecurityValidationResult = {
      isValid: true,
      errors: [],
      warnings: []
    };

    if (!input) {
      return result;
    }

    // Check for suspicious patterns
    this.suspiciousPatterns.forEach(pattern => {
      if (pattern.test(input)) {
        result.isValid = false;
        result.errors.push('Input contains potentially dangerous content');
      }
    });

    // Check for excessively long input
    if (input.length > 10000) {
      result.isValid = false;
      result.errors.push('Input is too long');
    }

    // Check for null bytes
    if (input.includes('\0')) {
      result.isValid = false;
      result.errors.push('Input contains null bytes');
    }

    // Warn about special characters
    if (/[<>"'&]/.test(input)) {
      result.warnings.push('Input contains special characters that may be encoded');
    }

    return result;
  }

  /**
   * Validates file security before upload
   */
  validateFileUpload(file: File): SecurityValidationResult {
    const result: SecurityValidationResult = {
      isValid: true,
      errors: [],
      warnings: []
    };

    // Check file size (10MB limit)
    const maxSize = 10 * 1024 * 1024;
    if (file.size > maxSize) {
      result.isValid = false;
      result.errors.push(`File size (${this.formatFileSize(file.size)}) exceeds maximum allowed size (${this.formatFileSize(maxSize)})`);
    }

    // Check minimum file size
    if (file.size < 1024) {
      result.isValid = false;
      result.errors.push('File is too small and may be corrupted');
    }

    // Check filename
    if (!file.name || file.name.trim().length === 0) {
      result.isValid = false;
      result.errors.push('File must have a valid name');
    }

    // Check for dangerous file extensions
    const extension = this.getFileExtension(file.name).toLowerCase();
    if (this.dangerousFileExtensions.includes(extension)) {
      result.isValid = false;
      result.errors.push(`File extension '${extension}' is not allowed for security reasons`);
    }

    // Check for allowed extensions
    const allowedExtensions = ['.jpg', '.jpeg', '.png', '.pdf'];
    if (!allowedExtensions.includes(extension)) {
      result.isValid = false;
      result.errors.push(`File extension '${extension}' is not allowed`);
    }

    // Check MIME type
    const allowedMimeTypes = ['image/jpeg', 'image/png', 'application/pdf'];
    if (!allowedMimeTypes.includes(file.type.toLowerCase())) {
      result.isValid = false;
      result.errors.push(`File type '${file.type}' is not supported`);
    }

    // Check for suspicious filename patterns
    if (this.hasSuspiciousFilename(file.name)) {
      result.isValid = false;
      result.errors.push('Filename contains suspicious patterns');
    }

    // Check for double extensions
    const extensionCount = (file.name.match(/\./g) || []).length;
    if (extensionCount > 1) {
      result.warnings.push('File has multiple extensions - ensure this is legitimate');
    }

    // Warn about very large files
    if (file.size > 5 * 1024 * 1024) {
      result.warnings.push('Large file detected - may take longer to process');
    }

    return result;
  }

  /**
   * Validates URL for security
   */
  validateUrl(url: string): SecurityValidationResult {
    const result: SecurityValidationResult = {
      isValid: true,
      errors: [],
      warnings: []
    };

    if (!url) {
      return result;
    }

    try {
      const urlObj = new URL(url);
      
      // Check protocol
      if (!['http:', 'https:'].includes(urlObj.protocol)) {
        result.isValid = false;
        result.errors.push('URL must use HTTP or HTTPS protocol');
      }

      // Check for suspicious patterns in URL
      if (this.suspiciousPatterns.some(pattern => pattern.test(url))) {
        result.isValid = false;
        result.errors.push('URL contains suspicious patterns');
      }

      // Warn about non-HTTPS URLs
      if (urlObj.protocol === 'http:') {
        result.warnings.push('URL uses insecure HTTP protocol');
      }

    } catch (error) {
      result.isValid = false;
      result.errors.push('Invalid URL format');
    }

    return result;
  }

  /**
   * Generates a secure token for client-side use
   */
  generateSecureToken(): string {
    const array = new Uint8Array(32);
    crypto.getRandomValues(array);
    return Array.from(array, byte => byte.toString(16).padStart(2, '0')).join('');
  }

  /**
   * Validates a security token format
   */
  validateSecurityToken(token: string): boolean {
    if (!token || token.length < 32) {
      return false;
    }

    // Check for valid hexadecimal characters
    return /^[a-fA-F0-9]+$/.test(token);
  }

  /**
   * Adds security headers to HTTP requests
   */
  addSecurityHeaders(request: HttpRequest<any>): HttpRequest<any> {
    return request.clone({
      setHeaders: {
        'X-Requested-With': 'XMLHttpRequest',
        'X-Content-Type-Options': 'nosniff',
        'X-Frame-Options': 'DENY'
      }
    });
  }

  /**
   * Validates form data before submission
   */
  validateFormData(formData: any): SecurityValidationResult {
    const result: SecurityValidationResult = {
      isValid: true,
      errors: [],
      warnings: []
    };

    if (!formData) {
      return result;
    }

    // Recursively validate all form fields
    this.validateObjectFields(formData, result, '');

    return result;
  }

  /**
   * Checks if the current environment is secure (HTTPS)
   */
  isSecureEnvironment(): boolean {
    return window.location.protocol === 'https:' || 
           window.location.hostname === 'localhost' ||
           window.location.hostname === '127.0.0.1';
  }

  /**
   * Logs security events for monitoring
   */
  logSecurityEvent(eventType: string, details: string): void {
    console.warn(`Security Event: ${eventType} - ${details}`);
    
    // In production, you might want to send this to a logging service
    // this.http.post('/api/security/log', { eventType, details, timestamp: new Date() }).subscribe();
  }

  // Private helper methods

  private getFileExtension(filename: string): string {
    const lastDotIndex = filename.lastIndexOf('.');
    return lastDotIndex !== -1 ? filename.substring(lastDotIndex) : '';
  }

  private hasSuspiciousFilename(filename: string): boolean {
    // Check for directory traversal
    if (filename.includes('..') || filename.includes('\\') || filename.includes('/')) {
      return true;
    }

    // Check for null bytes
    if (filename.includes('\0')) {
      return true;
    }

    // Check for Windows reserved names
    const reservedNames = ['CON', 'PRN', 'AUX', 'NUL', 'COM1', 'COM2', 'COM3', 'COM4', 'COM5', 'COM6', 'COM7', 'COM8', 'COM9', 'LPT1', 'LPT2', 'LPT3', 'LPT4', 'LPT5', 'LPT6', 'LPT7', 'LPT8', 'LPT9'];
    const nameWithoutExtension = filename.split('.')[0].toUpperCase();
    if (reservedNames.includes(nameWithoutExtension)) {
      return true;
    }

    return false;
  }

  private validateObjectFields(obj: any, result: SecurityValidationResult, path: string): void {
    for (const [key, value] of Object.entries(obj)) {
      const currentPath = path ? `${path}.${key}` : key;
      
      if (typeof value === 'string') {
        const validation = this.validateInput(value);
        if (!validation.isValid) {
          result.isValid = false;
          validation.errors.forEach(error => {
            result.errors.push(`${currentPath}: ${error}`);
          });
        }
        validation.warnings.forEach(warning => {
          result.warnings.push(`${currentPath}: ${warning}`);
        });
      } else if (typeof value === 'object' && value !== null) {
        this.validateObjectFields(value, result, currentPath);
      }
    }
  }

  private formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}