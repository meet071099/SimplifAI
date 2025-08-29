import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { FormStepperComponent } from './components/form-stepper/form-stepper.component';
import { PersonalInfoStepComponent } from './components/personal-info-step/personal-info-step.component';
import { DocumentUploadStepComponent } from './components/document-upload-step/document-upload-step.component';
import { ReviewStepComponent } from './components/review-step/review-step.component';
import { RecruiterDashboardComponent } from './components/recruiter-dashboard/recruiter-dashboard.component';
import { FormReviewComponent } from './components/form-review/form-review.component';

const routes: Routes = [
  { path: '', redirectTo: '/form/new', pathMatch: 'full' },
  { path: 'form/:id', component: FormStepperComponent },
  {
    path: 'recruiter',
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: RecruiterDashboardComponent },
      { path: 'forms/:id', component: FormReviewComponent }
    ]
  },
  { path: '**', redirectTo: '/form/new' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
