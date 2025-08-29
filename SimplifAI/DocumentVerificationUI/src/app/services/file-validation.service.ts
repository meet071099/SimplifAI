import { Injectable } from '@angular/core';

export interface FileValidationResult {
  isValid: boolean;
  errors: FileValidationError[];
  warnings: FileValidationWarning[];
}

export interface FileValidationError {
  code: string;
  message: string;
  severity: 'error' | 'warning';
}

export interface FileValidationWarning {
  code: string;
  message: string;
  suggestion: string;
}

export interface FileValidationConfig {
  maxSizeInMB: number;
  minSizeInKB: number;
  allowedTypes: string[];
  allowedExtensions: string[];
  maxFilenameLength: number;
}

@Injectable({
  providedIn: 'root'
})
export class FileValidationService {
  
  private readonly defaultConfig: FileValidationConfig = {
    maxSizeInMB: 10,
    minSizeInKB: 1,
    allowedTypes: ['image/jpeg', 'image/jpg', 'image/png', 'application/pdf'],
    allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf'],
    maxFilenameLength: 255
  };

  constructor() {}

  /**
   * Comprehensive file validation with detailed feedback
   */
  validateFile(file: File, config?: Partial<FileValidationConfig>): FileValidationResult {
    const validationConfig = { ...this.defaultConfig, ...config };
    const errors: FileValidationError[] = [];
    const warnings: FileValidationWarning[] = [];

    // Basic file existence check
    if (!file) {
      errors.push({
        code: 'NO_FILE',
        message: 'No file selected',
        severity: 'error'
      });
      return { isValid: false, errors, warnings };
    }

    // File size validation
    this.validateFileSize(file, validationConfig, errors, warnings);
    
    // File type validation
    this.validateFileType(file, validationConfig, errors, warnings);
    
    // File extension validation
    this.validateFileExtension(file, validationConfig, errors, warnings);
    
    // Filename validation
    this.validateFilename(file, validationConfig, errors, warnings);
    
    // Security validation
    this.validateFileSecurity(file, errors, warnings);
    
    // Quality checks and recommendations
    this.performQualityChecks(file, warnings);

    return {
      isValid: errors.length === 0,
      errors,
      warnings
    };
  }

  /**
   * Validate file size with detailed feedback
   */
  private validateFileSize(
    file: File, 
    config: FileValidationConfig, 
    errors: FileValidationError[], 
    warnings: FileValidationWarning[]
  ): void {
    const fileSizeInMB = file.size / (1024 * 1024);
    const fileSizeInKB = file.size / 1024;

    // Check maximum size
    if (fileSizeInMB > config.maxSizeInMB) {
      errors.push({
        code: 'FILE_TOO_LARGE',
        message: `File size (${fileSizeInMB.toFixed(2)}MB) exceeds the maximum allowed size of ${config.maxSizeInMB}MB`,
        severity: 'error'
      });
    }

    // Check minimum size
    if (fileSizeInKB < config.minSizeInKB) {
      errors.push({
        code: 'FILE_TOO_SMALL',
        message: `File size (${fileSizeInKB.toFixed(2)}KB) is below the minimum required size of ${config.minSizeInKB}KB`,
        severity: 'error'
      });
    }

    // Warn about large files that might take time to process
    if (fileSizeInMB > 5 && fileSizeInMB <= config.maxSizeInMB) {
      warnings.push({
        code: 'LARGE_FILE_WARNING',
        message: `Large file detected (${fileSizeInMB.toFixed(2)}MB)`,
        suggestion: 'Large files may take longer to upload and process. Consider compressing the image if possible.'
      });
    }

    // Warn about very small files that might be low quality
    if (fileSizeInKB < 50 && fileSizeInKB >= config.minSizeInKB) {
      warnings.push({
        code: 'SMALL_FILE_WARNING',
        message: `Small file detected (${fileSizeInKB.toFixed(2)}KB)`,
        suggestion: 'Very small files may be low quality. Ensure the document is clear and readable.'
      });
    }
  }

  /**
   * Validate file MIME type
   */
  private validateFileType(
    file: File, 
    config: FileValidationConfig, 
    errors: FileValidationError[], 
    warnings: FileValidationWarning[]
  ): void {
    const fileType = file.type.toLowerCase();

    if (!config.allowedTypes.includes(fileType)) {
      errors.push({
        code: 'INVALID_FILE_TYPE',
        message: `File type '${file.type}' is not supported`,
        severity: 'error'
      });
    }

    // Warn about potential MIME type spoofing
    if (!fileType || fileType === 'application/octet-stream') {
      warnings.push({
        code: 'UNKNOWN_MIME_TYPE',
        message: 'File type could not be determined',
        suggestion: 'Ensure the file is a valid document and not corrupted.'
      });
    }
  }

  /**
   * Validate file extension
   */
  private validateFileExtension(
    file: File, 
    config: FileValidationConfig, 
    errors: FileValidationError[], 
    warnings: FileValidationWarning[]
  ): void {
    const fileName = file.name.toLowerCase();
    const extension = '.' + fileName.split('.').pop();

    if (!config.allowedExtensions.includes(extension)) {
      errors.push({
        code: 'INVALID_FILE_EXTENSION',
        message: `File extension '${extension}' is not allowed`,
        severity: 'error'
      });
    }

    // Check for extension mismatch with MIME type
    const expectedExtensions = this.getExpectedExtensions(file.type);
    if (expectedExtensions.length > 0 && !expectedExtensions.includes(extension)) {
      warnings.push({
        code: 'EXTENSION_MISMATCH',
        message: `File extension '${extension}' doesn't match the file type '${file.type}'`,
        suggestion: 'Ensure the file extension matches the actual file type.'
      });
    }
  }

  /**
   * Validate filename
   */
  private validateFilename(
    file: File, 
    config: FileValidationConfig, 
    errors: FileValidationError[], 
    warnings: FileValidationWarning[]
  ): void {
    const fileName = file.name;

    // Check filename length
    if (fileName.length > config.maxFilenameLength) {
      errors.push({
        code: 'FILENAME_TOO_LONG',
        message: `Filename is too long (${fileName.length} characters). Maximum allowed: ${config.maxFilenameLength}`,
        severity: 'error'
      });
    }

    // Check for empty filename
    if (!fileName || fileName.trim().length === 0) {
      errors.push({
        code: 'EMPTY_FILENAME',
        message: 'File must have a valid name',
        severity: 'error'
      });
    }

    // Check for invalid characters in filename
    const invalidChars = /[<>:"/\\|?*\x00-\x1f]/;
    if (invalidChars.test(fileName)) {
      errors.push({
        code: 'INVALID_FILENAME_CHARACTERS',
        message: 'Filename contains invalid characters',
        severity: 'error'
      });
    }

    // Warn about special characters that might cause issues
    const specialChars = /[#%&{}\\<>*?/$!'":@+`|=]/;
    if (specialChars.test(fileName)) {
      warnings.push({
        code: 'SPECIAL_CHARACTERS_IN_FILENAME',
        message: 'Filename contains special characters',
        suggestion: 'Consider renaming the file to use only letters, numbers, hyphens, and underscores.'
      });
    }

    // Warn about very long filenames
    if (fileName.length > 100) {
      warnings.push({
        code: 'LONG_FILENAME',
        message: 'Filename is quite long',
        suggestion: 'Consider using a shorter, more descriptive filename.'
      });
    }
  }

  /**
   * Security validation
   */
  private validateFileSecurity(
    file: File, 
    errors: FileValidationError[], 
    warnings: FileValidationWarning[]
  ): void {
    const fileName = file.name.toLowerCase();

    // Check for dangerous file extensions
    const dangerousExtensions = [
      '.exe', '.bat', '.cmd', '.scr', '.com', '.pif', '.vbs', '.js', '.jar',
      '.app', '.deb', '.pkg', '.dmg', '.msi', '.run', '.sh', '.ps1'
    ];

    const hasDangerousExtension = dangerousExtensions.some(ext => fileName.endsWith(ext));
    if (hasDangerousExtension) {
      errors.push({
        code: 'DANGEROUS_FILE_TYPE',
        message: 'File type is not allowed for security reasons',
        severity: 'error'
      });
    }

    // Check for double extensions (potential security risk)
    const extensionCount = (fileName.match(/\./g) || []).length;
    if (extensionCount > 1) {
      const parts = fileName.split('.');
      if (parts.length > 2) {
        warnings.push({
          code: 'MULTIPLE_EXTENSIONS',
          message: 'File has multiple extensions',
          suggestion: 'Ensure this is a legitimate file and not a security risk.'
        });
      }
    }

    // Check for suspicious filename patterns
    const suspiciousPatterns = [
      /^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/i,
      /^\./,
      /\s+$/
    ];

    if (suspiciousPatterns.some(pattern => pattern.test(fileName))) {
      warnings.push({
        code: 'SUSPICIOUS_FILENAME',
        message: 'Filename follows a suspicious pattern',
        suggestion: 'Consider renaming the file to a more standard format.'
      });
    }
  }

  /**
   * Quality checks and recommendations
   */
  private performQualityChecks(file: File, warnings: FileValidationWarning[]): void {
    // Check if it's an image file for additional recommendations
    if (file.type.startsWith('image/')) {
      // Recommend optimal formats
      if (file.type === 'image/bmp' || file.type === 'image/tiff') {
        warnings.push({
          code: 'SUBOPTIMAL_IMAGE_FORMAT',
          message: 'Image format may not be optimal for web use',
          suggestion: 'Consider converting to JPEG or PNG for better compatibility and smaller file size.'
        });
      }

      // Check for very high resolution that might be unnecessary
      if (file.size > 8 * 1024 * 1024) { // 8MB
        warnings.push({
          code: 'HIGH_RESOLUTION_IMAGE',
          message: 'Image appears to be very high resolution',
          suggestion: 'For document verification, a resolution of 1200-2400 DPI is usually sufficient.'
        });
      }
    }

    // PDF-specific checks
    if (file.type === 'application/pdf') {
      // Warn about potentially large PDFs
      if (file.size > 5 * 1024 * 1024) { // 5MB
        warnings.push({
          code: 'LARGE_PDF',
          message: 'PDF file is quite large',
          suggestion: 'Consider compressing the PDF or ensuring it contains only the necessary pages.'
        });
      }
    }
  }

  /**
   * Get expected file extensions for a MIME type
   */
  private getExpectedExtensions(mimeType: string): string[] {
    const mimeToExtensions: { [key: string]: string[] } = {
      'image/jpeg': ['.jpg', '.jpeg'],
      'image/png': ['.png'],
      'application/pdf': ['.pdf']
    };

    return mimeToExtensions[mimeType.toLowerCase()] || [];
  }

  /**
   * Get user-friendly file size string
   */
  static formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  /**
   * Validate multiple files at once
   */
  validateFiles(files: File[], config?: Partial<FileValidationConfig>): { [filename: string]: FileValidationResult } {
    const results: { [filename: string]: FileValidationResult } = {};

    for (const file of files) {
      results[file.name] = this.validateFile(file, config);
    }

    return results;
  }
}