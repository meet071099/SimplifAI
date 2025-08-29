import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { FormReviewComponent } from './form-review.component';
import { RecruiterService } from '../../services/recruiter.service';
import { NotificationService } from '../../services/notification.service';

describe('FormReviewComponent', () => {
  let component: FormReviewComponent;
  let fixture: ComponentFixture<FormReviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ FormReviewComponent ],
      imports: [ 
        HttpClientTestingModule, 
        RouterTestingModule,
        FormsModule 
      ],
      providers: [
        RecruiterService,
        NotificationService,
        {
          provide: ActivatedRoute,
          useValue: {
            params: of({ id: 'test-form-id' })
          }
        }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FormReviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default values', () => {
    expect(component.form).toBeNull();
    expect(component.isLoading).toBe(false);
    expect(component.showStatusModal).toBe(false);
  });

  it('should have status options', () => {
    expect(component.statusOptions.length).toBeGreaterThan(0);
    expect(component.statusOptions.some(option => option.value === 'Approved')).toBeTruthy();
    expect(component.statusOptions.some(option => option.value === 'Rejected')).toBeTruthy();
  });
});