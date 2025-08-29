import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, timer } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { NotificationService } from '../services/notification.service';
import { SecurityService } from '../services/security.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly maxRetries = 3;
  private readonly retryDelay = 1000; // 1 second

  constructor(
    private notificationService: NotificationService,
    private securityService: SecurityService
  ) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    // Add security headers to all requests
    const secureReq = this.securityService.addSecurityHeaders(req);
    
    return next.handle(secureReq).pipe(
      retry({
        count: this.shouldRetry(req) ? this.maxRetries : 0,
        delay: (error: HttpErrorResponse, retryCount: number) => {
          // Only retry on network errors or 5xx server errors
          if (this.isRetryableError(error)) {
            const delay = this.retryDelay * Math.pow(2, retryCount - 1); // Exponential backoff
            console.log(`Retrying request (attempt ${retryCount}/${this.maxRetries}) after ${delay}ms`);
            return timer(delay);
          }
          return throwError(() => error);
        }
      }),
      catchError((error: HttpErrorResponse) => {
        this.handleError(error, req);
        return throwError(() => error);
      })
    );
  }

  private shouldRetry(req: HttpRequest<any>): boolean {
    // Only retry GET requests and non-file uploads
    return req.method === 'GET' || (req.method === 'POST' && !this.isFileUpload(req));
  }

  private isFileUpload(req: HttpRequest<any>): boolean {
    return req.body instanceof FormData;
  }

  private isRetryableError(error: HttpErrorResponse): boolean {
    // Retry on network errors (status 0) or server errors (5xx)
    return error.status === 0 || (error.status >= 500 && error.status < 600);
  }

  private handleError(error: HttpErrorResponse, req: HttpRequest<any>): void {
    let errorMessage = 'An unexpected error occurred';
    let errorTitle = 'Error';

    if (error.status === 0) {
      // Network error
      errorTitle = 'Network Error';
      errorMessage = 'Unable to connect to the server. Please check your internet connection and try again.';
      this.notificationService.showError(errorTitle, errorMessage);
    } else if (error.status >= 400 && error.status < 500) {
      // Client errors (4xx)
      this.handleClientError(error);
    } else if (error.status >= 500) {
      // Server errors (5xx)
      errorTitle = 'Server Error';
      errorMessage = 'The server encountered an error. Please try again later.';
      
      if (error.error?.message) {
        errorMessage = error.error.message;
      }
      
      this.notificationService.showError(errorTitle, errorMessage);
    }

    // Log error for debugging
    console.error('HTTP Error:', {
      status: error.status,
      statusText: error.statusText,
      url: req.url,
      method: req.method,
      error: error.error
    });
  }

  private handleClientError(error: HttpErrorResponse): void {
    const errorResponse = error.error;
    
    switch (error.status) {
      case 400:
        this.handle400Error(errorResponse);
        break;
      case 401:
        this.notificationService.showError(
          'Authentication Required',
          'You need to be authenticated to access this resource.'
        );
        break;
      case 403:
        this.notificationService.showError(
          'Access Denied',
          'You do not have permission to access this resource.'
        );
        break;
      case 404:
        this.notificationService.showError(
          'Not Found',
          errorResponse?.message || 'The requested resource was not found.'
        );
        break;
      case 409:
        this.notificationService.showError(
          'Conflict',
          errorResponse?.message || 'There was a conflict with the current state of the resource.'
        );
        break;
      case 413:
        this.notificationService.showError(
          'File Too Large',
          errorResponse?.message || 'The uploaded file is too large. Please choose a smaller file.'
        );
        break;
      case 415:
        this.notificationService.showError(
          'Unsupported File Type',
          errorResponse?.message || 'The file type is not supported. Please upload a valid file.'
        );
        break;
      case 422:
        this.handle422Error(errorResponse);
        break;
      default:
        this.notificationService.showError(
          'Request Error',
          errorResponse?.message || 'There was an error processing your request.'
        );
    }
  }

  private handle400Error(errorResponse: any): void {
    if (errorResponse?.validationErrors) {
      // Handle validation errors
      const validationMessages = this.formatValidationErrors(errorResponse.validationErrors);
      this.notificationService.showError(
        'Validation Error',
        validationMessages
      );
    } else {
      this.notificationService.showError(
        'Bad Request',
        errorResponse?.message || 'The request was invalid. Please check your input and try again.'
      );
    }
  }

  private handle422Error(errorResponse: any): void {
    if (errorResponse?.validationErrors) {
      const validationMessages = this.formatValidationErrors(errorResponse.validationErrors);
      this.notificationService.showError(
        'Validation Failed',
        validationMessages
      );
    } else {
      this.notificationService.showError(
        'Validation Error',
        errorResponse?.message || 'The submitted data could not be processed.'
      );
    }
  }

  private formatValidationErrors(validationErrors: { [key: string]: string[] }): string {
    const messages: string[] = [];
    
    for (const [field, errors] of Object.entries(validationErrors)) {
      const fieldName = this.formatFieldName(field);
      errors.forEach(error => {
        messages.push(`${fieldName}: ${error}`);
      });
    }
    
    return messages.join('\n');
  }

  private formatFieldName(fieldName: string): string {
    // Convert camelCase to readable format
    return fieldName
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, str => str.toUpperCase())
      .trim();
  }
}