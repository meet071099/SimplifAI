import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { 
  NotificationService, 
  NotificationMessage, 
  ConfirmationDialog 
} from '../../../services/notification.service';

@Component({
  selector: 'app-notification',
  templateUrl: './notification.component.html',
  styleUrls: ['./notification.component.css']
})
export class NotificationComponent implements OnInit, OnDestroy {
  notifications: NotificationMessage[] = [];
  confirmationDialog: ConfirmationDialog | null = null;
  
  private destroy$ = new Subject<void>();

  constructor(private notificationService: NotificationService) {}

  ngOnInit(): void {
    // Subscribe to notifications
    this.notificationService.notifications$
      .pipe(takeUntil(this.destroy$))
      .subscribe(notifications => {
        this.notifications = notifications;
      });

    // Subscribe to confirmation dialogs
    this.notificationService.confirmationDialog$
      .pipe(takeUntil(this.destroy$))
      .subscribe(dialog => {
        this.confirmationDialog = dialog;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Close a notification
   */
  closeNotification(id: string): void {
    this.notificationService.removeNotification(id);
  }

  /**
   * Execute notification action
   */
  executeAction(action: () => void, notificationId: string): void {
    action();
    this.closeNotification(notificationId);
  }

  /**
   * Confirm dialog action
   */
  confirmDialog(): void {
    if (this.confirmationDialog) {
      this.confirmationDialog.onConfirm();
      this.closeDialog();
    }
  }

  /**
   * Cancel dialog action
   */
  cancelDialog(): void {
    if (this.confirmationDialog) {
      if (this.confirmationDialog.onCancel) {
        this.confirmationDialog.onCancel();
      }
      this.closeDialog();
    }
  }

  /**
   * Close confirmation dialog
   */
  closeDialog(): void {
    this.notificationService.closeConfirmationDialog();
  }

  /**
   * Get notification icon based on type
   */
  getNotificationIcon(type: NotificationMessage['type']): string {
    switch (type) {
      case 'success':
        return '✓';
      case 'error':
        return '✗';
      case 'warning':
        return '⚠';
      case 'info':
      default:
        return 'ℹ';
    }
  }

  /**
   * Get dialog icon based on type
   */
  getDialogIcon(type: ConfirmationDialog['type']): string {
    switch (type) {
      case 'warning':
        return '⚠';
      case 'danger':
        return '⚠';
      case 'info':
      default:
        return '?';
    }
  }
}