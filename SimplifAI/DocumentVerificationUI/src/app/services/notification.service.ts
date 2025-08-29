import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface NotificationMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number; // in milliseconds, 0 means persistent
  actions?: NotificationAction[];
}

export interface NotificationAction {
  label: string;
  action: () => void;
  style?: 'primary' | 'secondary' | 'danger';
}

export interface ConfirmationDialog {
  id: string;
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  onConfirm: () => void;
  onCancel?: () => void;
  type?: 'info' | 'warning' | 'danger';
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private notificationsSubject = new BehaviorSubject<NotificationMessage[]>([]);
  private confirmationDialogSubject = new BehaviorSubject<ConfirmationDialog | null>(null);

  public notifications$ = this.notificationsSubject.asObservable();
  public confirmationDialog$ = this.confirmationDialogSubject.asObservable();

  constructor() {}

  /**
   * Show a success notification
   */
  showSuccess(title: string, message: string, duration: number = 5000): void {
    this.addNotification({
      type: 'success',
      title,
      message,
      duration
    });
  }

  /**
   * Show an error notification
   */
  showError(title: string, message: string, duration: number = 0): void {
    this.addNotification({
      type: 'error',
      title,
      message,
      duration
    });
  }

  /**
   * Show a warning notification
   */
  showWarning(title: string, message: string, duration: number = 8000): void {
    this.addNotification({
      type: 'warning',
      title,
      message,
      duration
    });
  }

  /**
   * Show an info notification
   */
  showInfo(title: string, message: string, duration: number = 5000): void {
    this.addNotification({
      type: 'info',
      title,
      message,
      duration
    });
  }

  /**
   * Show notification with custom actions
   */
  showNotificationWithActions(
    type: NotificationMessage['type'],
    title: string,
    message: string,
    actions: NotificationAction[],
    duration: number = 0
  ): void {
    this.addNotification({
      type,
      title,
      message,
      actions,
      duration
    });
  }

  /**
   * Show document blur detection feedback
   */
  showBlurDetectionFeedback(documentType: string, onRetry: () => void): void {
    this.showNotificationWithActions(
      'error',
      'Blurred Document Detected',
      `The ${documentType} appears blurred or unclear. Please upload a clearer image for better verification results.`,
      [
        {
          label: 'Upload New Document',
          action: onRetry,
          style: 'primary'
        }
      ]
    );
  }

  /**
   * Show document type mismatch feedback
   */
  showTypeMismatchFeedback(
    expectedType: string, 
    detectedType: string, 
    onRetry: () => void,
    onProceed?: () => void
  ): void {
    const actions: NotificationAction[] = [
      {
        label: 'Upload Correct Document',
        action: onRetry,
        style: 'primary'
      }
    ];

    if (onProceed) {
      actions.push({
        label: 'Proceed Anyway',
        action: onProceed,
        style: 'secondary'
      });
    }

    this.showNotificationWithActions(
      'warning',
      'Document Type Mismatch',
      `Expected ${expectedType} but detected ${detectedType || 'different document type'}. Please upload the correct document type.`,
      actions
    );
  }

  /**
   * Show confidence score confirmation dialog
   */
  showConfidenceConfirmation(
    documentType: string,
    confidenceScore: number,
    onConfirm: () => void,
    onReplace: () => void
  ): void {
    let message: string;
    let type: ConfirmationDialog['type'] = 'info';

    if (confidenceScore >= 50 && confidenceScore < 85) {
      message = `The ${documentType} has a moderate confidence score of ${confidenceScore}%. The document appears valid but may require manual review. Do you want to proceed with this document?`;
      type = 'warning';
    } else if (confidenceScore < 50) {
      message = `The ${documentType} has a low confidence score of ${confidenceScore}%. The document quality or authenticity may be questionable. Do you want to proceed with this document or upload a different one?`;
      type = 'danger';
    } else {
      // This shouldn't happen for confirmation dialogs, but handle it
      message = `The ${documentType} has been verified with ${confidenceScore}% confidence.`;
    }

    this.showConfirmationDialog({
      title: 'Document Verification Confirmation',
      message,
      confirmText: 'Proceed with Document',
      cancelText: 'Upload Different Document',
      onConfirm,
      onCancel: onReplace,
      type
    });
  }

  /**
   * Show document replacement confirmation
   */
  showReplacementConfirmation(documentType: string, onConfirm: () => void): void {
    this.showConfirmationDialog({
      title: 'Replace Document',
      message: `Are you sure you want to replace the current ${documentType}? This action cannot be undone.`,
      confirmText: 'Replace Document',
      cancelText: 'Keep Current',
      onConfirm,
      type: 'warning'
    });
  }

  /**
   * Show verification retry confirmation
   */
  showRetryConfirmation(documentType: string, onConfirm: () => void): void {
    this.showConfirmationDialog({
      title: 'Retry Verification',
      message: `Do you want to retry verification for the ${documentType}? This may take a few moments.`,
      confirmText: 'Retry Verification',
      cancelText: 'Cancel',
      onConfirm,
      type: 'info'
    });
  }

  /**
   * Add a notification to the queue
   */
  private addNotification(notification: Omit<NotificationMessage, 'id'>): void {
    const id = this.generateId();
    const fullNotification: NotificationMessage = {
      id,
      ...notification
    };

    const currentNotifications = this.notificationsSubject.value;
    this.notificationsSubject.next([...currentNotifications, fullNotification]);

    // Auto-remove notification after duration
    if (notification.duration && notification.duration > 0) {
      setTimeout(() => {
        this.removeNotification(id);
      }, notification.duration);
    }
  }

  /**
   * Remove a notification by ID
   */
  removeNotification(id: string): void {
    const currentNotifications = this.notificationsSubject.value;
    const filtered = currentNotifications.filter(n => n.id !== id);
    this.notificationsSubject.next(filtered);
  }

  /**
   * Clear all notifications
   */
  clearAllNotifications(): void {
    this.notificationsSubject.next([]);
  }

  /**
   * Show confirmation dialog
   */
  showConfirmationDialog(dialog: Omit<ConfirmationDialog, 'id'>): void {
    const fullDialog: ConfirmationDialog = {
      id: this.generateId(),
      confirmText: 'Confirm',
      cancelText: 'Cancel',
      type: 'info',
      ...dialog
    };

    this.confirmationDialogSubject.next(fullDialog);
  }

  /**
   * Close confirmation dialog
   */
  closeConfirmationDialog(): void {
    this.confirmationDialogSubject.next(null);
  }

  /**
   * Show form initialization feedback
   */
  showFormInitializing(message: string = 'Initializing form...'): void {
    this.showInfo('Form Initialization', message, 0);
  }

  /**
   * Show form initialization success
   */
  showFormInitialized(message: string = 'Form is ready for data entry'): void {
    this.showSuccess('Form Ready', message, 3000);
  }

  /**
   * Show form initialization error with retry
   */
  showFormInitializationError(error: string, onRetry: () => void, onDemoMode?: () => void): void {
    const actions: NotificationAction[] = [
      {
        label: 'Retry',
        action: onRetry,
        style: 'primary'
      }
    ];

    if (onDemoMode) {
      actions.push({
        label: 'Continue in Demo Mode',
        action: onDemoMode,
        style: 'secondary'
      });
    }

    this.showNotificationWithActions(
      'error',
      'Form Initialization Failed',
      error || 'Unable to initialize form. Please try again or continue in demo mode.',
      actions
    );
  }

  /**
   * Show save operation feedback
   */
  showSaveInProgress(message: string = 'Saving your information...'): void {
    this.showInfo('Saving', message, 0);
  }

  /**
   * Show save success feedback
   */
  showSaveSuccess(message: string = 'Your information has been saved successfully'): void {
    this.showSuccess('Saved', message, 3000);
  }

  /**
   * Show save error with retry
   */
  showSaveError(error: string, onRetry: () => void): void {
    this.showNotificationWithActions(
      'error',
      'Save Failed',
      error || 'Unable to save your information. Please try again.',
      [
        {
          label: 'Retry Save',
          action: onRetry,
          style: 'primary'
        }
      ]
    );
  }

  /**
   * Show demo mode notification
   */
  showDemoModeActive(message: string = 'You are in demo mode. Changes will not be saved to the server.'): void {
    this.showWarning('Demo Mode Active', message, 0);
  }

  /**
   * Show network error with retry
   */
  showNetworkError(onRetry: () => void, onDemoMode?: () => void): void {
    const actions: NotificationAction[] = [
      {
        label: 'Retry',
        action: onRetry,
        style: 'primary'
      }
    ];

    if (onDemoMode) {
      actions.push({
        label: 'Continue Offline',
        action: onDemoMode,
        style: 'secondary'
      });
    }

    this.showNotificationWithActions(
      'error',
      'Network Connection Error',
      'Unable to connect to the server. Please check your internet connection and try again.',
      actions
    );
  }

  /**
   * Generate unique ID for notifications and dialogs
   */
  private generateId(): string {
    return Date.now().toString(36) + Math.random().toString(36).substring(2);
  }
}