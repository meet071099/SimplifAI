import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';

import { FileUploadComponent, FileUploadEvent } from './file-upload.component';
import { FileValidationService } from '../../../services/file-validation.service';
import { SecurityService } from '../../../services/security.service';
import { NotificationService } from '../../../services/notification.service';

describe('FileUploadComponent', () => {
  let component: FileUploadComponent;
  let fixture: ComponentFixture<FileUploadComponent>;
  let fileValidationService: jasmine.SpyObj<FileValidationService>;
  let securityService: jasmine.SpyObj<SecurityService>;
  let notificationService: jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    const fileValidationServiceSpy = jasmine.createSpyObj('FileValidationService', [
      'validateFile'
    ]);
    const securityServiceSpy = jasmine.createSpyObj('SecurityService', [
      'validateFileUpload', 'logSecurityEvent'
    ]);
    const notificationServiceSpy = jasmine.createSpyObj('NotificationService', [
      'showError', 'showWarning', 'showSuccess'
    ]);

    await TestBed.configureTestingModule({
      declarations: [ FileUploadComponent ],
      imports: [ HttpClientTestingModule ],
      providers: [
        { provide: FileValidationService, useValue: fileValidationServiceSpy },
        { provide: SecurityService, useValue: securityServiceSpy },
        { provide: NotificationService, useValue: notificationServiceSpy }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FileUploadComponent);
    component = fixture.componentInstance;
    fileValidationService = TestBed.inject(FileValidationService) as jasmine.SpyObj<FileValidationService>;
    securityService = TestBed.inject(SecurityService) as jasmine.SpyObj<SecurityService>;
    notificationService = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default values', () => {
    expect(component.isDragOver).toBe(false);
    expect(component.uploadProgress).toBe(0);
    expect(component.isUploading).toBe(false);
    expect(component.previewUrl).toBeNull();
    expect(component.documentType).toBe('');
    expect(component.disabled).toBe(false);
  });

  it('should handle drag over event', () => {
    const event = new DragEvent('dragover');
    spyOn(event, 'preventDefault');
    spyOn(event, 'stopPropagation');

    component.onDragOver(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(component.isDragOver).toBe(true);
  });

  it('should handle drag leave event', () => {
    const event = new DragEvent('dragleave');
    spyOn(event, 'preventDefault');
    spyOn(event, 'stopPropagation');

    component.onDragLeave(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(component.isDragOver).toBe(false);
  });

  it('should handle file drop', () => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(file);
    
    const event = new DragEvent('drop');
    Object.defineProperty(event, 'dataTransfer', {
      value: dataTransfer
    });
    
    spyOn(event, 'preventDefault');
    spyOn(event, 'stopPropagation');
    spyOn(component as any, 'handleFile');

    component.onDrop(event);

    expect(event.preventDefault).toHaveBeenCalled();
    expect(event.stopPropagation).toHaveBeenCalled();
    expect(component.isDragOver).toBe(false);
    expect((component as any).handleFile).toHaveBeenCalledWith(file);
  });

  it('should handle file input change', () => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const input = document.createElement('input');
    input.type = 'file';
    
    const dataTransfer = new DataTransfer();
    dataTransfer.items.add(file);
    input.files = dataTransfer.files;

    const event = { target: input } as any;
    spyOn(component as any, 'handleFile');

    component.onFileInputChange(event);

    expect((component as any).handleFile).toHaveBeenCalledWith(file);
  });

  it('should validate and handle valid file', () => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    component.documentType = 'Passport';
    
    securityService.validateFileUpload.and.returnValue({ 
      isValid: true, 
      errors: [], 
      warnings: [] 
    });
    fileValidationService.validateFile.and.returnValue({ 
      isValid: true, 
      errors: [], 
      warnings: [] 
    });
    
    spyOn(component as any, 'generatePreview');
    spyOn(component as any, 'simulateUpload');

    (component as any).handleFile(file);

    expect(securityService.validateFileUpload).toHaveBeenCalledWith(file);
    expect(fileValidationService.validateFile).toHaveBeenCalledWith(file, {
      maxSizeInMB: component.maxSizeInMB,
      allowedExtensions: component.acceptedTypes.split(',').map(t => t.trim())
    });
    expect((component as any).generatePreview).toHaveBeenCalledWith(file);
    expect((component as any).simulateUpload).toHaveBeenCalledWith(file);
  });

  it('should handle security validation failure', () => {
    const file = new File(['test'], 'test.exe', { type: 'application/exe' });
    
    securityService.validateFileUpload.and.returnValue({ 
      isValid: false, 
      errors: ['Executable files not allowed'], 
      warnings: [] 
    });

    (component as any).handleFile(file);

    expect(notificationService.showError).toHaveBeenCalledWith(
      'Security Validation Failed', 
      'Executable files not allowed'
    );
    expect(securityService.logSecurityEvent).toHaveBeenCalledWith(
      'InvalidFileUpload', 
      'File security validation failed: Executable files not allowed'
    );
  });

  it('should handle file validation failure', () => {
    const file = new File(['test'], 'test.txt', { type: 'text/plain' });
    
    securityService.validateFileUpload.and.returnValue({ 
      isValid: true, 
      errors: [], 
      warnings: [] 
    });
    fileValidationService.validateFile.and.returnValue({ 
      isValid: false, 
      errors: [{ message: 'Invalid file type', code: 'INVALID_TYPE' }], 
      warnings: [] 
    });

    (component as any).handleFile(file);

    expect(notificationService.showError).toHaveBeenCalledWith(
      'File Validation Error', 
      'Invalid file type'
    );
  });

  it('should remove file correctly', () => {
    component.previewUrl = 'data:image/jpeg;base64,test';
    component.uploadProgress = 50;
    component.isUploading = true;
    component.documentType = 'Passport';
    
    spyOn(component.fileRemoved, 'emit');

    component.removeFile();

    expect(component.previewUrl).toBeNull();
    expect(component.uploadProgress).toBe(0);
    expect(component.isUploading).toBe(false);
    expect(component.fileRemoved.emit).toHaveBeenCalledWith('Passport');
  });

  it('should open file dialog when not disabled', () => {
    const fileInput = jasmine.createSpyObj('HTMLInputElement', ['click']);
    component.fileInput = { nativeElement: fileInput };
    component.disabled = false;

    component.openFileDialog();

    expect(fileInput.click).toHaveBeenCalled();
  });

  it('should not open file dialog when disabled', () => {
    const fileInput = jasmine.createSpyObj('HTMLInputElement', ['click']);
    component.fileInput = { nativeElement: fileInput };
    component.disabled = true;

    component.openFileDialog();

    expect(fileInput.click).not.toHaveBeenCalled();
  });

  it('should get correct file icon', () => {
    expect(component.getFileIcon('document.pdf')).toBe('📄');
    expect(component.getFileIcon('image.jpg')).toBe('🖼️');
    expect(component.getFileIcon('image.jpeg')).toBe('🖼️');
    expect(component.getFileIcon('image.png')).toBe('🖼️');
    expect(component.getFileIcon('document.txt')).toBe('📎');
  });

  it('should generate preview for image files', () => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    spyOn(window, 'FileReader').and.returnValue({
      readAsDataURL: jasmine.createSpy('readAsDataURL'),
      onload: null
    } as any);

    (component as any).generatePreview(file);

    expect(window.FileReader).toHaveBeenCalled();
  });

  it('should not generate preview for non-image files', () => {
    const file = new File(['test'], 'test.pdf', { type: 'application/pdf' });

    (component as any).generatePreview(file);

    expect(component.previewUrl).toBeNull();
  });

  it('should simulate upload progress', (done) => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    component.documentType = 'Passport';
    spyOn(component.fileSelected, 'emit');

    (component as any).simulateUpload(file);

    expect(component.isUploading).toBe(true);
    expect(component.uploadProgress).toBe(0);

    // Wait for upload simulation to complete
    setTimeout(() => {
      expect(component.uploadProgress).toBe(100);
      expect(component.isUploading).toBe(false);
      expect(component.fileSelected.emit).toHaveBeenCalledWith({
        file: file,
        documentType: 'Passport'
      });
      done();
    }, 1100);
  });
});