import { Component } from '@angular/core';
import { NotificationService } from '../../../services/notification.service';
import { ErrorAction } from '../error-message/error-message.component';

@Component({
  selector: 'app-form-feedback-demo',
  templateUrl: './form-feedback-demo.component.html',
  styleUrls: ['./form-feedback-demo.component.css']
})
export class FormFeedbackDemoComponent {
  // Demo state
  isInitializing = false;
  isDemoMode = false;
  showError = false;
  saveInProgress = false;
  initializationProgress = 0;

  // Error component properties
  errorActions: ErrorAction[] = [
    {
      label: 'Retry Connection',
      action: () => this.retryInitialization(),
      style: 'primary',
      loading: false
    },
    {
      label: 'Continue Offline',
      action: () => this.enterDemoMode(),
      style: 'secondary'
    }
  ];

  constructor(private notificationService: NotificationService) {}

  // Demo Methods
  startFormInitialization(): void {
    this.isInitializing = true;
    this.initializationProgress = 0;
    this.showError = false;
    this.isDemoMode = false;

    // Simulate initialization progress
    const progressInterval = setInterval(() => {
      this.initializationProgress += 10;
      if (this.initializationProgress >= 100) {
        clearInterval(progressInterval);
        setTimeout(() => {
          this.isInitializing = false;
          this.notificationService.showFormInitialized();
        }, 500);
      }
    }, 200);

    this.notificationService.showFormInitializing('Setting up your form...');
  }

  simulateInitializationError(): void {
    this.isInitializing = false;
    this.showError = true;
    this.notificationService.showFormInitializationError(
      'Unable to connect to the server. Please check your internet connection.',
      () => this.retryInitialization(),
      () => this.enterDemoMode()
    );
  }

  retryInitialization(): void {
    this.showError = false;
    this.startFormInitialization();
  }

  enterDemoMode(): void {
    this.showError = false;
    this.isDemoMode = true;
    this.notificationService.showDemoModeActive();
  }

  simulateSaveOperation(): void {
    this.saveInProgress = true;
    this.notificationService.showSaveInProgress();

    setTimeout(() => {
      this.saveInProgress = false;
      if (Math.random() > 0.5) {
        this.notificationService.showSaveSuccess();
      } else {
        this.notificationService.showSaveError(
          'Network timeout occurred while saving.',
          () => this.simulateSaveOperation()
        );
      }
    }, 2000);
  }

  showNetworkError(): void {
    this.notificationService.showNetworkError(
      () => this.retryInitialization(),
      () => this.enterDemoMode()
    );
  }

  clearAllNotifications(): void {
    this.notificationService.clearAllNotifications();
  }

  resetDemo(): void {
    this.isInitializing = false;
    this.isDemoMode = false;
    this.showError = false;
    this.saveInProgress = false;
    this.initializationProgress = 0;
    this.clearAllNotifications();
  }
}