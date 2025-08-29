import { Component, Input, Output, EventEmitter, ElementRef, ViewChild } from '@angular/core';
import { FileValidationService, FileValidationResult } from '../../../services/file-validation.service';
import { SecurityService, SecurityValidationResult } from '../../../services/security.service';
import { NotificationService } from '../../../services/notification.service';

export interface FileUploadEvent {
  file: File;
  documentType: string;
}

@Component({
  selector: 'app-file-upload',
  templateUrl: './file-upload.component.html',
  styleUrls: ['./file-upload.component.css']
})
export class FileUploadComponent {
  @Input() documentType: string = '';
  @Input() acceptedTypes: string = '.jpg,.jpeg,.png,.pdf';
  @Input() maxSizeInMB: number = 10;
  @Input() disabled: boolean = false;
  @Input() currentFile: File | null = null;
  
  @Output() fileSelected = new EventEmitter<FileUploadEvent>();
  @Output() fileRemoved = new EventEmitter<string>();
  
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  
  isDragOver = false;
  uploadProgress = 0;
  isUploading = false;
  previewUrl: string | null = null;
  
  constructor(
    private fileValidationService: FileValidationService,
    private securityService: SecurityService,
    private notificationService: NotificationService
  ) {}
  
  ngOnInit() {
    if (this.currentFile) {
      this.generatePreview(this.currentFile);
    }
  }
  
  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!this.disabled) {
      this.isDragOver = true;
    }
  }
  
  onDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }
  
  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
    
    if (this.disabled) return;
    
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.handleFile(files[0]);
    }
  }
  
  onFileInputChange(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.handleFile(input.files[0]);
    }
  }
  
  private handleFile(file: File) {
    if (!this.validateFile(file)) {
      return;
    }
    
    this.generatePreview(file);
    this.simulateUpload(file);
  }
  
  private validateFile(file: File): boolean {
    // First, perform security validation
    const securityValidation = this.securityService.validateFileUpload(file);
    if (!securityValidation.isValid) {
      const errorMessages = securityValidation.errors.join('\n');
      this.notificationService.showError('Security Validation Failed', errorMessages);
      this.securityService.logSecurityEvent('InvalidFileUpload', `File security validation failed: ${errorMessages}`);
      return false;
    }

    // Show security warnings if any
    // if (securityValidation.warnings.length > 0) {
    //   securityValidation.warnings.forEach(warning => {
    //     this.notificationService.showWarning('Security Warning', warning);
    //   });
    // }

    // Then perform detailed file validation
    const validationResult = this.fileValidationService.validateFile(file, {
      maxSizeInMB: this.maxSizeInMB,
      allowedExtensions: this.acceptedTypes.split(',').map(t => t.trim())
    });

    if (!validationResult.isValid) {
      // Show detailed error messages
      const errorMessages = validationResult.errors.map(error => error.message).join('\n');
      this.notificationService.showError('File Validation Error', errorMessages);
      return false;
    }

    // Show warnings if any
    // if (validationResult.warnings.length > 0) {
    //   validationResult.warnings.forEach(warning => {
    //     this.notificationService.showWarning(
    //       'File Warning', 
    //       `${warning.message}\n${warning.suggestion}`
    //     );
    //   });
    // }

    return true;
  }
  
  private generatePreview(file: File) {
    if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (e) => {
        this.previewUrl = e.target?.result as string;
      };
      reader.readAsDataURL(file);
    } else {
      this.previewUrl = null;
    }
  }
  
  private simulateUpload(file: File) {
    this.isUploading = true;
    this.uploadProgress = 0;
    
    const interval = setInterval(() => {
      this.uploadProgress += 10;
      if (this.uploadProgress >= 100) {
        clearInterval(interval);
        this.isUploading = false;
        this.fileSelected.emit({ file, documentType: this.documentType });
      }
    }, 100);
  }
  
  openFileDialog() {
    if (!this.disabled) {
      this.fileInput.nativeElement.click();
    }
  }
  
  removeFile() {
    this.previewUrl = null;
    this.uploadProgress = 0;
    this.isUploading = false;
    this.fileInput.nativeElement.value = '';
    this.fileRemoved.emit(this.documentType);
  }
  
  getFileIcon(fileName: string): string {
    const extension = fileName.split('.').pop()?.toLowerCase();
    switch (extension) {
      case 'pdf':
        return '📄';
      case 'jpg':
      case 'jpeg':
      case 'png':
        return '🖼️';
      default:
        return '📎';
    }
  }
}