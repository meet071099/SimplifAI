import { Component, Input, Output, EventEmitter } from '@angular/core';

export type AlertType = 'success' | 'warning' | 'error' | 'info';

export interface AlertAction {
  label: string;
  action: string;
  primary?: boolean;
}

@Component({
  selector: 'app-alert',
  templateUrl: './alert.component.html',
  styleUrls: ['./alert.component.css']
})
export class AlertComponent {
  @Input() type: AlertType = 'info';
  @Input() title: string = '';
  @Input() message: string = '';
  @Input() dismissible: boolean = true;
  @Input() actions: AlertAction[] = [];
  @Input() showIcon: boolean = true;
  @Input() animated: boolean = true;
  
  @Output() dismissed = new EventEmitter<void>();
  @Output() actionClicked = new EventEmitter<string>();
  
  isVisible = true;
  
  get alertIcon(): string {
    switch (this.type) {
      case 'success':
        return '✅';
      case 'warning':
        return '⚠️';
      case 'error':
        return '❌';
      case 'info':
        return 'ℹ️';
      default:
        return 'ℹ️';
    }
  }
  
  get alertClass(): string {
    return `alert-${this.type}`;
  }
  
  onDismiss() {
    if (this.dismissible) {
      this.isVisible = false;
      setTimeout(() => {
        this.dismissed.emit();
      }, 300); // Wait for animation to complete
    }
  }
  
  onActionClick(action: string) {
    this.actionClicked.emit(action);
  }
}