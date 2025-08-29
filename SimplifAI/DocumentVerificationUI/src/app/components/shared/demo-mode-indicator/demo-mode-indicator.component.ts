import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-demo-mode-indicator',
  templateUrl: './demo-mode-indicator.component.html',
  styleUrls: ['./demo-mode-indicator.component.css']
})
export class DemoModeIndicatorComponent {
  @Input() visible: boolean = false;
  @Input() message: string = 'Demo Mode - Data will not be saved';
  @Input() position: 'top' | 'bottom' | 'inline' = 'top';
  @Input() variant: 'banner' | 'badge' | 'card' = 'banner';
  @Input() showIcon: boolean = true;
  @Input() dismissible: boolean = false;
  
  dismissed: boolean = false;

  get containerClass(): string {
    let classes = ['demo-indicator'];
    
    if (this.visible && !this.dismissed) {
      classes.push('visible');
    }
    
    classes.push(`position-${this.position}`);
    classes.push(`variant-${this.variant}`);
    
    return classes.join(' ');
  }

  dismiss(): void {
    if (this.dismissible) {
      this.dismissed = true;
    }
  }

  show(): void {
    this.dismissed = false;
  }
}