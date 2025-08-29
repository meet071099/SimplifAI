import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  templateUrl: './loading-spinner.component.html',
  styleUrls: ['./loading-spinner.component.css']
})
export class LoadingSpinnerComponent {
  @Input() size: 'small' | 'medium' | 'large' = 'medium';
  @Input() message: string = 'Loading...';
  @Input() showMessage: boolean = true;
  @Input() color: 'primary' | 'secondary' | 'success' | 'warning' | 'danger' = 'primary';
  @Input() overlay: boolean = false;
  @Input() centered: boolean = true;
  
  get spinnerClass(): string {
    return `spinner-${this.size} spinner-${this.color}`;
  }
  
  get containerClass(): string {
    let classes = ['loading-container'];
    
    if (this.overlay) {
      classes.push('overlay');
    }
    
    if (this.centered) {
      classes.push('centered');
    }
    
    return classes.join(' ');
  }
}