import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConfidenceScoreComponent } from './confidence-score.component';

describe('ConfidenceScoreComponent', () => {
  let component: ConfidenceScoreComponent;
  let fixture: ComponentFixture<ConfidenceScoreComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ConfidenceScoreComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ConfidenceScoreComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should calculate confidence level correctly', () => {
    component.score = 95;
    expect(component.confidenceLevel).toBe('green');

    component.score = 75;
    expect(component.confidenceLevel).toBe('yellow');

    component.score = 45;
    expect(component.confidenceLevel).toBe('red');
  });

  it('should return correct tick icon', () => {
    component.score = 95;
    expect(component.tickIcon).toBe('✓');

    component.score = 75;
    expect(component.tickIcon).toBe('⚠');

    component.score = 45;
    expect(component.tickIcon).toBe('✗');
  });

  it('should return correct status text', () => {
    component.score = 95;
    expect(component.statusText).toBe('Verified');

    component.score = 75;
    expect(component.statusText).toBe('Review Required');

    component.score = 45;
    expect(component.statusText).toBe('Failed');
  });

  it('should return correct confidence description', () => {
    component.score = 95;
    expect(component.confidenceDescription).toBe('Document verification passed with high confidence');

    component.score = 75;
    expect(component.confidenceDescription).toBe('Document verification requires manual review');

    component.score = 45;
    expect(component.confidenceDescription).toBe('Document verification failed or has low confidence');
  });

  it('should handle input properties correctly', () => {
    component.score = 85;
    component.level = 'yellow';
    component.showPercentage = false;
    component.showLabel = true;
    component.size = 'large';
    component.animated = false;

    expect(component.score).toBe(85);
    expect(component.level).toBe('yellow');
    expect(component.showPercentage).toBe(false);
    expect(component.showLabel).toBe(true);
    expect(component.size).toBe('large');
    expect(component.animated).toBe(false);
  });

  it('should have default values', () => {
    expect(component.score).toBe(0);
    expect(component.level).toBe('red');
    expect(component.showPercentage).toBe(true);
    expect(component.showLabel).toBe(false);
    expect(component.size).toBe('medium');
    expect(component.animated).toBe(true);
  });
});