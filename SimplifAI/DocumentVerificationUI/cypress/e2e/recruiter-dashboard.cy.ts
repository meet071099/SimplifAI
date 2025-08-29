describe('Recruiter Dashboard', () => {
  const mockForms = [
    {
      id: 'form-1',
      uniqueUrl: 'form-url-1',
      status: 'Submitted',
      createdAt: '2024-01-15T10:00:00Z',
      submittedAt: '2024-01-15T14:30:00Z',
      personalInfo: {
        firstName: 'John',
        lastName: 'Doe',
        email: 'john.doe@example.com'
      },
      documents: [
        {
          id: 'doc-1',
          documentType: 'Passport',
          verificationStatus: 'Verified',
          confidenceScore: 95.5,
          statusColor: 'green'
        }
      ]
    },
    {
      id: 'form-2',
      uniqueUrl: 'form-url-2',
      status: 'Pending',
      createdAt: '2024-01-16T09:00:00Z',
      submittedAt: null,
      personalInfo: {
        firstName: 'Jane',
        lastName: 'Smith',
        email: 'jane.smith@example.com'
      },
      documents: []
    }
  ];

  beforeEach(() => {
    // Mock API responses
    cy.intercept('GET', '/api/recruiter/forms', {
      statusCode: 200,
      body: mockForms
    }).as('getForms');

    cy.intercept('GET', '/api/recruiter/forms/form-1', {
      statusCode: 200,
      body: mockForms[0]
    }).as('getFormDetails');

    cy.intercept('POST', '/api/recruiter/forms/form-1/approve', {
      statusCode: 200,
      body: { status: 'Approved' }
    }).as('approveForm');

    cy.intercept('POST', '/api/recruiter/forms/form-1/reject', {
      statusCode: 200,
      body: { status: 'Rejected' }
    }).as('rejectForm');

    // Visit recruiter dashboard
    cy.visit('/recruiter/dashboard');
    cy.wait('@getForms');
  });

  it('should display list of submitted forms', () => {
    // Verify dashboard loads
    cy.dataCy('recruiter-dashboard').should('be.visible');
    cy.dataCy('dashboard-title').should('contain', 'Recruiter Dashboard');

    // Verify forms list
    cy.dataCy('forms-list').should('be.visible');
    cy.dataCy('form-item').should('have.length', 2);

    // Verify first form details
    cy.dataCy('form-item').first().within(() => {
      cy.dataCy('candidate-name').should('contain', 'John Doe');
      cy.dataCy('form-status').should('contain', 'Submitted');
      cy.dataCy('submission-date').should('be.visible');
    });

    // Verify second form details
    cy.dataCy('form-item').last().within(() => {
      cy.dataCy('candidate-name').should('contain', 'Jane Smith');
      cy.dataCy('form-status').should('contain', 'Pending');
    });
  });

  it('should filter forms by status', () => {
    // Test status filter
    cy.dataCy('status-filter').select('Submitted');
    cy.dataCy('form-item').should('have.length', 1);
    cy.dataCy('form-item').first().should('contain', 'John Doe');

    cy.dataCy('status-filter').select('Pending');
    cy.dataCy('form-item').should('have.length', 1);
    cy.dataCy('form-item').first().should('contain', 'Jane Smith');

    cy.dataCy('status-filter').select('All');
    cy.dataCy('form-item').should('have.length', 2);
  });

  it('should search forms by candidate name', () => {
    // Test search functionality
    cy.dataCy('search-input').type('John');
    cy.dataCy('form-item').should('have.length', 1);
    cy.dataCy('form-item').should('contain', 'John Doe');

    cy.dataCy('search-input').clear().type('Smith');
    cy.dataCy('form-item').should('have.length', 1);
    cy.dataCy('form-item').should('contain', 'Jane Smith');

    cy.dataCy('search-input').clear();
    cy.dataCy('form-item').should('have.length', 2);
  });

  it('should open form details modal', () => {
    // Click on first form to view details
    cy.dataCy('form-item').first().click();
    cy.wait('@getFormDetails');

    // Verify modal opens
    cy.dataCy('form-details-modal').should('be.visible');
    cy.dataCy('modal-title').should('contain', 'Form Details - John Doe');

    // Verify personal information section
    cy.dataCy('personal-info-section').within(() => {
      cy.dataCy('firstName').should('contain', 'John');
      cy.dataCy('lastName').should('contain', 'Doe');
      cy.dataCy('email').should('contain', 'john.doe@example.com');
    });

    // Verify documents section
    cy.dataCy('documents-section').within(() => {
      cy.dataCy('document-item').should('have.length', 1);
      cy.dataCy('document-type').should('contain', 'Passport');
      cy.dataCy('verification-status').should('contain', 'Verified');
      cy.dataCy('confidence-score').should('contain', '95.5%');
    });
  });

  it('should approve a form', () => {
    // Open form details
    cy.dataCy('form-item').first().click();
    cy.wait('@getFormDetails');

    // Approve the form
    cy.dataCy('approve-btn').should('be.visible').click();
    
    // Confirm approval
    cy.dataCy('confirm-approve-btn').click();
    cy.wait('@approveForm');

    // Verify success message
    cy.dataCy('success-message').should('be.visible');
    cy.dataCy('success-message').should('contain', 'Form approved successfully');

    // Modal should close
    cy.dataCy('form-details-modal').should('not.exist');
  });

  it('should reject a form with reason', () => {
    // Open form details
    cy.dataCy('form-item').first().click();
    cy.wait('@getFormDetails');

    // Reject the form
    cy.dataCy('reject-btn').should('be.visible').click();

    // Enter rejection reason
    cy.dataCy('rejection-reason-modal').should('be.visible');
    cy.dataCy('rejection-reason-textarea').type('Documents are not clear enough for verification.');
    
    // Confirm rejection
    cy.dataCy('confirm-reject-btn').click();
    cy.wait('@rejectForm');

    // Verify success message
    cy.dataCy('success-message').should('be.visible');
    cy.dataCy('success-message').should('contain', 'Form rejected successfully');
  });

  it('should display document verification details', () => {
    // Open form details
    cy.dataCy('form-item').first().click();
    cy.wait('@getFormDetails');

    // Click on document to view details
    cy.dataCy('document-item').first().click();

    // Verify document details modal
    cy.dataCy('document-details-modal').should('be.visible');
    cy.dataCy('document-image').should('be.visible');
    cy.dataCy('verification-details').should('be.visible');
    
    // Verify confidence score display
    cy.dataCy('confidence-score-display').should('contain', '95.5%');
    cy.dataCy('status-indicator').should('have.class', 'green');
  });

  it('should handle forms with low confidence documents', () => {
    // Mock form with low confidence document
    const lowConfidenceForm = {
      ...mockForms[0],
      documents: [
        {
          id: 'doc-low',
          documentType: 'Passport',
          verificationStatus: 'Review Required',
          confidenceScore: 45.2,
          statusColor: 'red'
        }
      ]
    };

    cy.intercept('GET', '/api/recruiter/forms/form-1', {
      statusCode: 200,
      body: lowConfidenceForm
    }).as('getLowConfidenceForm');

    // Open form details
    cy.dataCy('form-item').first().click();
    cy.wait('@getLowConfidenceForm');

    // Verify low confidence indicators
    cy.dataCy('documents-section').within(() => {
      cy.dataCy('verification-status').should('contain', 'Review Required');
      cy.dataCy('confidence-score').should('contain', '45.2%');
      cy.dataCy('status-indicator').should('have.class', 'red');
      cy.dataCy('manual-review-badge').should('be.visible');
    });

    // Verify warning message
    cy.dataCy('low-confidence-warning').should('be.visible');
    cy.dataCy('low-confidence-warning').should('contain', 'requires manual review');
  });

  it('should sort forms by different criteria', () => {
    // Test sorting by submission date
    cy.dataCy('sort-dropdown').select('Submission Date (Newest)');
    cy.dataCy('form-item').first().should('contain', 'Jane Smith'); // Newer submission

    cy.dataCy('sort-dropdown').select('Submission Date (Oldest)');
    cy.dataCy('form-item').first().should('contain', 'John Doe'); // Older submission

    // Test sorting by candidate name
    cy.dataCy('sort-dropdown').select('Candidate Name (A-Z)');
    cy.dataCy('form-item').first().should('contain', 'Jane Smith'); // Jane comes before John

    cy.dataCy('sort-dropdown').select('Candidate Name (Z-A)');
    cy.dataCy('form-item').first().should('contain', 'John Doe'); // John comes after Jane
  });

  it('should handle empty state when no forms exist', () => {
    // Mock empty forms response
    cy.intercept('GET', '/api/recruiter/forms', {
      statusCode: 200,
      body: []
    }).as('getEmptyForms');

    cy.reload();
    cy.wait('@getEmptyForms');

    // Verify empty state
    cy.dataCy('empty-state').should('be.visible');
    cy.dataCy('empty-state-message').should('contain', 'No forms found');
    cy.dataCy('empty-state-icon').should('be.visible');
  });

  it('should handle API errors gracefully', () => {
    // Mock API error
    cy.intercept('GET', '/api/recruiter/forms', {
      statusCode: 500,
      body: { error: 'Internal server error' }
    }).as('getFormsError');

    cy.reload();
    cy.wait('@getFormsError');

    // Verify error state
    cy.dataCy('error-message').should('be.visible');
    cy.dataCy('error-message').should('contain', 'Failed to load forms');
    cy.dataCy('retry-btn').should('be.visible');

    // Test retry functionality
    cy.intercept('GET', '/api/recruiter/forms', {
      statusCode: 200,
      body: mockForms
    }).as('getFormsRetry');

    cy.dataCy('retry-btn').click();
    cy.wait('@getFormsRetry');

    // Verify forms load after retry
    cy.dataCy('forms-list').should('be.visible');
    cy.dataCy('form-item').should('have.length', 2);
  });

  it('should export forms data', () => {
    // Test export functionality
    cy.dataCy('export-btn').click();
    cy.dataCy('export-dropdown').should('be.visible');

    // Test CSV export
    cy.dataCy('export-csv-btn').click();
    // Note: In a real test, you'd verify the download occurred
    // For now, we just verify the button works

    // Test PDF export
    cy.dataCy('export-btn').click();
    cy.dataCy('export-pdf-btn').click();
  });
});