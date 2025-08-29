import { ErrorHandler, Injectable, NgZone } from '@angular/core';
import { NotificationService } from './notification.service';

@Injectable()
export class GlobalErrorHandlerService implements ErrorHandler {
  constructor(
    private notificationService: NotificationService,
    private ngZone: NgZone
  ) {}

  handleError(error: any): void {
    // Log the error for debugging
    console.error('Global error caught:', error);

    // Run inside Angular zone to ensure change detection works
    this.ngZone.run(() => {
      let errorMessage = 'An unexpected error occurred';
      let errorTitle = 'Application Error';

      if (error?.message) {
        errorMessage = error.message;
      }

      // Handle specific error types
      if (error?.name === 'ChunkLoadError') {
        errorTitle = 'Loading Error';
        errorMessage = 'Failed to load application resources. Please refresh the page and try again.';
        
        this.notificationService.showNotificationWithActions(
          'error',
          errorTitle,
          errorMessage,
          [
            {
              label: 'Refresh Page',
              action: () => window.location.reload(),
              style: 'primary'
            }
          ]
        );
        return;
      }

      if (error?.name === 'TypeError' && error?.message?.includes('fetch')) {
        errorTitle = 'Network Error';
        errorMessage = 'Unable to connect to the server. Please check your internet connection.';
      }

      // Show error notification
      this.notificationService.showError(errorTitle, errorMessage);
    });

    // Re-throw the error to maintain default behavior for debugging
    if (console && console.error) {
      console.error('Unhandled error:', error);
    }
  }
}