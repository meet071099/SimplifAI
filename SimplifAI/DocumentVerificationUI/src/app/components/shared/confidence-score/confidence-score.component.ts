import { Component, Input } from '@angular/core';

export type ConfidenceLevel = 'green' | 'yellow' | 'red';

@Component({
  selector: 'app-confidence-score',
  templateUrl: './confidence-score.component.html',
  styleUrls: ['./confidence-score.component.css']
})
export class ConfidenceScoreComponent {
  @Input() score: number = 0;
  @Input() level: ConfidenceLevel = 'red';
  @Input() showPercentage: boolean = true;
  @Input() showLabel: boolean = false;
  @Input() size: 'small' | 'medium' | 'large' = 'medium';
  @Input() animated: boolean = true;
  
  get confidenceLevel(): ConfidenceLevel {
    if (this.score >= 85) return 'green';
    if (this.score >= 50) return 'yellow';
    return 'red';
  }
  
  get tickIcon(): string {
    switch (this.confidenceLevel) {
      case 'green':
        return '✓';
      case 'yellow':
        return '⚠';
      case 'red':
        return '✗';
      default:
        return '?';
    }
  }
  
  get statusText(): string {
    switch (this.confidenceLevel) {
      case 'green':
        return 'Verified';
      case 'yellow':
        return 'Review Required';
      case 'red':
        return 'Failed';
      default:
        return 'Unknown';
    }
  }
  
  get confidenceDescription(): string {
    switch (this.confidenceLevel) {
      case 'green':
        return 'Document verification passed with high confidence';
      case 'yellow':
        return 'Document verification requires manual review';
      case 'red':
        return 'Document verification failed or has low confidence';
      default:
        return 'Document verification status unknown';
    }
  }
}