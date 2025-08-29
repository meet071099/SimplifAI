import { Component, Input, Output, EventEmitter } from '@angular/core';

export interface ErrorAction {
  label: string;
  action: () => void;
  style?: 'primary' | 'secondary' | 'danger';
  loading?: boolean;
}

@Component({
  selector: 'app-error-message',
  templateUrl: './error-message.component.html',
  styleUrls: ['./error-message.component.css']
})
export class ErrorMessageComponent {
  @Input() visible: boolean = false;
  @Input() title: string = 'Something went wrong';
  @Input() message: string = 'An unexpected error occurred. Please try again.';
  @Input() errorCode?: string;
  @Input() errorDetails?: string;
  @Input() variant: 'inline' | 'modal' | 'banner' = 'inline';
  @Input() severity: 'error' | 'warning' | 'info' = 'error';
  @Input() showIcon: boolean = true;
  @Input() dismissible: boolean = true;
  @Input() retryable: boolean = true;
  @Input() retryLabel: string = 'Try Again';
  @Input() retryLoading: boolean = false;
  @Input() actions: ErrorAction[] = [];
  @Input() showDetails: boolean = false;
  @Input() collapsible: boolean = true;
  
  @Output() retry = new EventEmitter<void>();
  @Output() dismiss = new EventEmitter<void>();
  @Output() actionClick = new EventEmitter<ErrorAction>();

  detailsExpanded: boolean = false;

  get containerClass(): string {
    let classes = ['error-container'];
    
    if (this.visible) {
      classes.push('visible');
    }
    
    classes.push(`variant-${this.variant}`);
    classes.push(`severity-${this.severity}`);
    
    return classes.join(' ');
  }

  get iconName(): string {
    switch (this.severity) {
      case 'error':
        return 'error';
      case 'warning':
        return 'warning';
      case 'info':
        return 'info';
      default:
        return 'error';
    }
  }

  onRetry(): void {
    if (!this.retryLoading) {
      this.retry.emit();
    }
  }

  onDismiss(): void {
    this.dismiss.emit();
  }

  onActionClick(action: ErrorAction): void {
    if (!action.loading) {
      this.actionClick.emit(action);
      action.action();
    }
  }

  toggleDetails(): void {
    if (this.collapsible) {
      this.detailsExpanded = !this.detailsExpanded;
    }
  }

  getDefaultActions(): ErrorAction[] {
    const defaultActions: ErrorAction[] = [];
    
    if (this.retryable) {
      defaultActions.push({
        label: this.retryLabel,
        action: () => this.onRetry(),
        style: 'primary',
        loading: this.retryLoading
      });
    }
    
    return defaultActions;
  }

  getAllActions(): ErrorAction[] {
    return [...this.getDefaultActions(), ...this.actions];
  }
}