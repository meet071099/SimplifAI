import { NgModule, ErrorHandler } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { FormStepperComponent } from './components/form-stepper/form-stepper.component';
import { PersonalInfoStepComponent } from './components/personal-info-step/personal-info-step.component';
import { DocumentUploadStepComponent } from './components/document-upload-step/document-upload-step.component';
import { ReviewStepComponent } from './components/review-step/review-step.component';
import { FileUploadComponent } from './components/shared/file-upload/file-upload.component';
import { ConfidenceScoreComponent } from './components/shared/confidence-score/confidence-score.component';
import { DocumentVerificationComponent } from './components/shared/document-verification/document-verification.component';
import { AlertComponent } from './components/shared/alert/alert.component';
import { LoadingSpinnerComponent } from './components/shared/loading-spinner/loading-spinner.component';
import { NotificationComponent } from './components/shared/notification/notification.component';
import { DemoModeIndicatorComponent } from './components/shared/demo-mode-indicator/demo-mode-indicator.component';
import { ErrorMessageComponent } from './components/shared/error-message/error-message.component';
import { FormLoadingSpinnerComponent } from './components/shared/form-loading-spinner/form-loading-spinner.component';
import { RecruiterDashboardComponent } from './components/recruiter-dashboard/recruiter-dashboard.component';
import { FormReviewComponent } from './components/form-review/form-review.component';

// Import error handling services and interceptors
import { ErrorInterceptor } from './interceptors/error.interceptor';
import { GlobalErrorHandlerService } from './services/global-error-handler.service';

@NgModule({
  declarations: [
    AppComponent,
    FormStepperComponent,
    PersonalInfoStepComponent,
    DocumentUploadStepComponent,
    ReviewStepComponent,
    FileUploadComponent,
    ConfidenceScoreComponent,
    DocumentVerificationComponent,
    AlertComponent,
    LoadingSpinnerComponent,
    NotificationComponent,
    DemoModeIndicatorComponent,
    ErrorMessageComponent,
    FormLoadingSpinnerComponent,
    RecruiterDashboardComponent,
    FormReviewComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    HttpClientModule,
    ReactiveFormsModule,
    FormsModule
  ],
  providers: [
    // Global error handler
    {
      provide: ErrorHandler,
      useClass: GlobalErrorHandlerService
    },
    // HTTP error interceptor
    {
      provide: HTTP_INTERCEPTORS,
      useClass: ErrorInterceptor,
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
