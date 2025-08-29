describe('Document Verification Form Submission Flow', () => {
  const testFormId = 'test-form-123';
  const personalInfo = {
    firstName: 'John',
    lastName: 'Doe',
    email: 'john.doe@example.com',
    phone: '123-456-7890',
    address: '123 Main Street, City, State 12345',
    dateOfBirth: '1990-01-01'
  };

  beforeEach(() => {
    // Mock API responses
    cy.intercept('GET', `/api/forms/${testFormId}`, {
      statusCode: 200,
      body: {
        id: testFormId,
        status: 'Pending',
        personalInfo: null,
        documents: []
      }
    }).as('getForm');

    cy.intercept('PUT', `/api/forms/${testFormId}/personal-info`, {
      statusCode: 200,
      body: personalInfo
    }).as('updatePersonalInfo');

    cy.intercept('POST', '/api/documents/upload', {
      statusCode: 200,
      body: {
        documentId: 'doc-123',
        verificationStatus: 'verified',
        confidenceScore: 95.5,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'green',
        message: 'Document verified successfully'
      }
    }).as('uploadDocument');

    cy.intercept('POST', `/api/forms/${testFormId}/submit`, {
      statusCode: 200,
      body: {
        formId: testFormId,
        status: 'Submitted',
        submittedAt: new Date().toISOString()
      }
    }).as('submitForm');

    // Visit the form page
    cy.visit(`/form/${testFormId}`);
    cy.wait('@getForm');
  });

  it('should complete the entire form submission flow', () => {
    // Step 1: Verify initial state
    cy.dataCy('form-stepper').should('be.visible');
    cy.dataCy('step-indicator-1').should('have.class', 'active');
    cy.dataCy('progress-bar').should('contain', '33%');

    // Step 2: Fill personal information
    cy.dataCy('personal-info-form').should('be.visible');
    cy.fillPersonalInfo(personalInfo);

    // Verify form validation
    cy.dataCy('firstName').should('have.value', personalInfo.firstName);
    cy.dataCy('email').should('have.value', personalInfo.email);

    // Navigate to next step
    cy.dataCy('next-step-btn').should('not.be.disabled').click();
    cy.wait('@updatePersonalInfo');

    // Step 3: Upload documents
    cy.dataCy('step-indicator-2').should('have.class', 'active');
    cy.dataCy('document-upload-section').should('be.visible');

    // Upload passport document
    cy.dataCy('passport-upload-area').should('be.visible');
    cy.uploadFile('passport.jpg');
    cy.wait('@uploadDocument');

    // Verify document verification result
    cy.dataCy('confidence-score').should('contain', '95.5%');
    cy.dataCy('verification-status').should('have.class', 'green');
    cy.dataCy('status-message').should('contain', 'Document verified successfully');

    // Navigate to review step
    cy.dataCy('next-step-btn').should('not.be.disabled').click();

    // Step 4: Review and submit
    cy.dataCy('step-indicator-3').should('have.class', 'active');
    cy.dataCy('review-section').should('be.visible');

    // Verify personal info in review
    cy.dataCy('review-firstName').should('contain', personalInfo.firstName);
    cy.dataCy('review-lastName').should('contain', personalInfo.lastName);
    cy.dataCy('review-email').should('contain', personalInfo.email);

    // Verify document info in review
    cy.dataCy('review-documents').should('contain', 'Passport');
    cy.dataCy('review-document-status').should('contain', 'Verified');

    // Submit form
    cy.dataCy('submit-form-btn').should('not.be.disabled').click();
    cy.wait('@submitForm');

    // Verify submission success
    cy.dataCy('success-message').should('be.visible');
    cy.dataCy('success-message').should('contain', 'Form submitted successfully');
  });

  it('should handle form validation errors', () => {
    // Try to proceed without filling required fields
    cy.dataCy('next-step-btn').click();

    // Verify validation errors are shown
    cy.dataCy('firstName-error').should('be.visible');
    cy.dataCy('lastName-error').should('be.visible');
    cy.dataCy('email-error').should('be.visible');

    // Fill invalid email
    cy.dataCy('email').type('invalid-email');
    cy.dataCy('email-error').should('contain', 'Please enter a valid email');

    // Fill valid data
    cy.fillPersonalInfo(personalInfo);

    // Verify errors are cleared
    cy.dataCy('firstName-error').should('not.exist');
    cy.dataCy('email-error').should('not.exist');

    // Should be able to proceed now
    cy.dataCy('next-step-btn').should('not.be.disabled');
  });

  it('should handle document upload errors', () => {
    // Fill personal info and navigate to document upload
    cy.fillPersonalInfo(personalInfo);
    cy.dataCy('next-step-btn').click();
    cy.wait('@updatePersonalInfo');

    // Mock upload error
    cy.intercept('POST', '/api/documents/upload', {
      statusCode: 400,
      body: { error: 'Invalid file type' }
    }).as('uploadError');

    // Try to upload invalid file
    cy.uploadFile('document.txt', 'text/plain');
    cy.wait('@uploadError');

    // Verify error message is shown
    cy.dataCy('upload-error').should('be.visible');
    cy.dataCy('upload-error').should('contain', 'Invalid file type');

    // Should not be able to proceed without valid documents
    cy.dataCy('next-step-btn').should('be.disabled');
  });

  it('should handle low confidence document verification', () => {
    // Fill personal info and navigate to document upload
    cy.fillPersonalInfo(personalInfo);
    cy.dataCy('next-step-btn').click();
    cy.wait('@updatePersonalInfo');

    // Mock low confidence response
    cy.intercept('POST', '/api/documents/upload', {
      statusCode: 200,
      body: {
        documentId: 'doc-123',
        verificationStatus: 'review_required',
        confidenceScore: 45.2,
        isBlurred: false,
        isCorrectType: true,
        statusColor: 'red',
        message: 'Low confidence score. Please confirm or re-upload.',
        requiresUserConfirmation: true
      }
    }).as('lowConfidenceUpload');

    // Upload document
    cy.uploadFile('passport.jpg');
    cy.wait('@lowConfidenceUpload');

    // Verify low confidence UI
    cy.dataCy('confidence-score').should('contain', '45.2%');
    cy.dataCy('verification-status').should('have.class', 'red');
    cy.dataCy('confirmation-dialog').should('be.visible');

    // User can confirm the document
    cy.dataCy('confirm-document-btn').click();

    // Should be able to proceed after confirmation
    cy.dataCy('next-step-btn').should('not.be.disabled');
  });

  it('should handle blurred document detection', () => {
    // Fill personal info and navigate to document upload
    cy.fillPersonalInfo(personalInfo);
    cy.dataCy('next-step-btn').click();
    cy.wait('@updatePersonalInfo');

    // Mock blurred document response
    cy.intercept('POST', '/api/documents/upload', {
      statusCode: 200,
      body: {
        documentId: 'doc-123',
        verificationStatus: 'failed',
        confidenceScore: 30.0,
        isBlurred: true,
        isCorrectType: true,
        statusColor: 'red',
        message: 'Document appears blurred. Please upload a clear image.',
        requiresUserConfirmation: false
      }
    }).as('blurredUpload');

    // Upload blurred document
    cy.uploadFile('blurred-passport.jpg');
    cy.wait('@blurredUpload');

    // Verify blurred document UI
    cy.dataCy('verification-status').should('have.class', 'red');
    cy.dataCy('status-message').should('contain', 'blurred');
    cy.dataCy('re-upload-btn').should('be.visible');

    // Should not be able to proceed with blurred document
    cy.dataCy('next-step-btn').should('be.disabled');
  });

  it('should allow navigation between completed steps', () => {
    // Complete personal info step
    cy.fillPersonalInfo(personalInfo);
    cy.dataCy('next-step-btn').click();
    cy.wait('@updatePersonalInfo');

    // Complete document upload step
    cy.uploadFile('passport.jpg');
    cy.wait('@uploadDocument');
    cy.dataCy('next-step-btn').click();

    // Now in review step, should be able to go back
    cy.dataCy('step-indicator-1').click();
    cy.dataCy('personal-info-form').should('be.visible');

    // Should be able to navigate to any completed step
    cy.dataCy('step-indicator-2').click();
    cy.dataCy('document-upload-section').should('be.visible');

    cy.dataCy('step-indicator-3').click();
    cy.dataCy('review-section').should('be.visible');
  });

  it('should show loading states during operations', () => {
    // Mock slow API response
    cy.intercept('PUT', `/api/forms/${testFormId}/personal-info`, {
      statusCode: 200,
      body: personalInfo,
      delay: 2000
    }).as('slowUpdatePersonalInfo');

    cy.fillPersonalInfo(personalInfo);
    cy.dataCy('next-step-btn').click();

    // Verify loading state
    cy.dataCy('loading-spinner').should('be.visible');
    cy.dataCy('next-step-btn').should('be.disabled');

    cy.wait('@slowUpdatePersonalInfo');

    // Verify loading state is cleared
    cy.dataCy('loading-spinner').should('not.exist');
  });
});