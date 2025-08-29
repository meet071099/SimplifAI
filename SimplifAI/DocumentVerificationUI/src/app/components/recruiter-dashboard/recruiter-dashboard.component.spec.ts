import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { FormsModule } from '@angular/forms';

import { RecruiterDashboardComponent } from './recruiter-dashboard.component';
import { RecruiterService } from '../../services/recruiter.service';
import { NotificationService } from '../../services/notification.service';

describe('RecruiterDashboardComponent', () => {
  let component: RecruiterDashboardComponent;
  let fixture: ComponentFixture<RecruiterDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ RecruiterDashboardComponent ],
      imports: [ 
        HttpClientTestingModule, 
        RouterTestingModule,
        FormsModule 
      ],
      providers: [
        RecruiterService,
        NotificationService
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RecruiterDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with default values', () => {
    expect(component.forms).toEqual([]);
    expect(component.currentPage).toBe(1);
    expect(component.pageSize).toBe(10);
    expect(component.searchTerm).toBe('');
    expect(component.statusFilter).toBe('');
  });

  it('should have status options', () => {
    expect(component.statusOptions.length).toBeGreaterThan(0);
    expect(component.statusOptions[0].value).toBe('');
    expect(component.statusOptions[0].label).toBe('All Statuses');
  });
});