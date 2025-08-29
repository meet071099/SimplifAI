import { TestBed } from '@angular/core/testing';
import { FileValidationService, FileValidationOptions } from './file-validation.service';

describe('FileValidationService', () => {
  let service: FileValidationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(FileValidationService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should validate file size correctly', () => {
    const validFile = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const largeFile = new File([new ArrayBuffer(11 * 1024 * 1024)], 'large.jpg', { type: 'image/jpeg' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const validResult = service.validateFile(validFile, options);
    expect(validResult.isValid).toBe(true);

    const invalidResult = service.validateFile(largeFile, options);
    expect(invalidResult.isValid).toBe(false);
    expect(invalidResult.errors.some(e => e.code === 'FILE_TOO_LARGE')).toBe(true);
  });

  it('should validate file type correctly', () => {
    const validFile = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    const invalidFile = new File(['test'], 'test.exe', { type: 'application/exe' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const validResult = service.validateFile(validFile, options);
    expect(validResult.isValid).toBe(true);

    const invalidResult = service.validateFile(invalidFile, options);
    expect(invalidResult.isValid).toBe(false);
    expect(invalidResult.errors.some(e => e.code === 'INVALID_FILE_TYPE')).toBe(true);
  });

  it('should validate file name correctly', () => {
    const validFile = new File(['test'], 'test-document.jpg', { type: 'image/jpeg' });
    const invalidFile = new File(['test'], 'test<>file.jpg', { type: 'image/jpeg' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const validResult = service.validateFile(validFile, options);
    expect(validResult.isValid).toBe(true);

    const invalidResult = service.validateFile(invalidFile, options);
    expect(invalidResult.isValid).toBe(false);
    expect(invalidResult.errors.some(e => e.code === 'INVALID_FILE_NAME')).toBe(true);
  });

  it('should detect empty files', () => {
    const emptyFile = new File([''], 'empty.jpg', { type: 'image/jpeg' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const result = service.validateFile(emptyFile, options);
    expect(result.isValid).toBe(false);
    expect(result.errors.some(e => e.code === 'EMPTY_FILE')).toBe(true);
  });

  it('should provide warnings for large files', () => {
    const largeFile = new File([new ArrayBuffer(8 * 1024 * 1024)], 'large.jpg', { type: 'image/jpeg' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const result = service.validateFile(largeFile, options);
    expect(result.isValid).toBe(true);
    expect(result.warnings.some(w => w.code === 'LARGE_FILE_SIZE')).toBe(true);
  });

  it('should validate MIME type matches extension', () => {
    const mismatchFile = new File(['test'], 'test.jpg', { type: 'application/pdf' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const result = service.validateFile(mismatchFile, options);
    expect(result.warnings.some(w => w.code === 'MIME_TYPE_MISMATCH')).toBe(true);
  });

  it('should handle files without extensions', () => {
    const noExtFile = new File(['test'], 'testfile', { type: 'image/jpeg' });

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const result = service.validateFile(noExtFile, options);
    expect(result.isValid).toBe(false);
    expect(result.errors.some(e => e.code === 'NO_FILE_EXTENSION')).toBe(true);
  });

  it('should format file size correctly', () => {
    expect(service.formatFileSize(1024)).toBe('1.0 KB');
    expect(service.formatFileSize(1048576)).toBe('1.0 MB');
    expect(service.formatFileSize(1073741824)).toBe('1.0 GB');
    expect(service.formatFileSize(500)).toBe('500 B');
    expect(service.formatFileSize(1536)).toBe('1.5 KB');
  });

  it('should get file extension correctly', () => {
    expect(service.getFileExtension('test.jpg')).toBe('.jpg');
    expect(service.getFileExtension('document.PDF')).toBe('.pdf');
    expect(service.getFileExtension('file.tar.gz')).toBe('.gz');
    expect(service.getFileExtension('noextension')).toBe('');
  });

  it('should validate multiple files', () => {
    const files = [
      new File(['test1'], 'test1.jpg', { type: 'image/jpeg' }),
      new File(['test2'], 'test2.pdf', { type: 'application/pdf' }),
      new File([new ArrayBuffer(11 * 1024 * 1024)], 'large.jpg', { type: 'image/jpeg' })
    ];

    const options: FileValidationOptions = {
      maxSizeInMB: 10,
      allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
    };

    const results = service.validateMultipleFiles(files, options);
    
    expect(results.length).toBe(3);
    expect(results[0].isValid).toBe(true);
    expect(results[1].isValid).toBe(true);
    expect(results[2].isValid).toBe(false);
  });

  it('should use default options when none provided', () => {
    const file = new File(['test'], 'test.jpg', { type: 'image/jpeg' });
    
    const result = service.validateFile(file);
    expect(result.isValid).toBe(true);
  });
});