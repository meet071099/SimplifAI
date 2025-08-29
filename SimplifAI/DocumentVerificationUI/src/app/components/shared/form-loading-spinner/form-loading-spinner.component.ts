import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-form-loading-spinner',
  templateUrl: './form-loading-spinner.component.html',
  styleUrls: ['./form-loading-spinner.component.css']
})
export class FormLoadingSpinnerComponent {
  @Input() visible: boolean = false;
  @Input() message: string = 'Initializing form...';
  @Input() submessage?: string;
  @Input() progress?: number; // 0-100 for progress bar
  @Input() variant: 'overlay' | 'inline' | 'card' = 'overlay';
  @Input() size: 'small' | 'medium' | 'large' = 'medium';
  @Input() showProgress: boolean = false;
  @Input() animated: boolean = true;

  get containerClass(): string {
    let classes = ['form-loading-container'];
    
    if (this.visible) {
      classes.push('visible');
    }
    
    classes.push(`variant-${this.variant}`);
    classes.push(`size-${this.size}`);
    
    if (this.animated) {
      classes.push('animated');
    }
    
    return classes.join(' ');
  }

  get progressPercentage(): number {
    return Math.max(0, Math.min(100, this.progress || 0));
  }
}