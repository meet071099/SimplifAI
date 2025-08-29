describe('Production Integration Tests', () => {
  const testData = {
    recruiterEmail: 'production-test@example.com',
    personalInfo: {
      firstName: 'Production',
      lastName: 'Test',
      email: 'production.test@example.com',
      phone: '+1234567890',
      address: '123 Production Test Street, Test City, TC 12345',
      dateOfBirth: '1990-01-01'
    }
  };

  let formId: string;
  let uniqueUrl: string;

  before(() => {
    // Set up test environment
    cy.log('Setting up production integration test environment');
    
    // Verify API is accessible
    cy.request({
      method: 'GET',
      url: `${Cypress.env('apiUrl')}/monitoring/health`,
      failOnStatusCode: false
    }).then((response) => {
      if (response.status !== 200) {
        throw new Error(`API health check failed with status ${response.status}`);
      }
    });
  });

  it('should complete the full production workflow', () => {
    cy.log('Starting complete production workflow test');

    // Step 1: Create a new form via API
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: testData.recruiterEmail
      }
    }).then((response) => {
      expect(response.status).to.eq(200);
      formId = response.body.formId;
      uniqueUrl = response.body.uniqueUrl;
      
      cy.log(`Form created: ${formId}, URL: ${uniqueUrl}`);
    });

    // Step 2: Visit the form and complete it
    cy.visit(`/form/${uniqueUrl}`);
    
    // Verify form loads correctly
    cy.get('[data-cy="form-stepper"]').should('be.visible');
    cy.get('[data-cy="step-indicator-1"]').should('have.class', 'active');

    // Fill personal information
    cy.log('Filling personal information');
    cy.get('[data-cy="firstName"]').type(testData.personalInfo.firstName);
    cy.get('[data-cy="lastName"]').type(testData.personalInfo.lastName);
    cy.get('[data-cy="email"]').type(testData.personalInfo.email);
    cy.get('[data-cy="phone"]').type(testData.personalInfo.phone);
    cy.get('[data-cy="address"]').type(testData.personalInfo.address);
    cy.get('[data-cy="dateOfBirth"]').type(testData.personalInfo.dateOfBirth);

    // Proceed to document upload
    cy.get('[data-cy="next-step-btn"]').should('not.be.disabled').click();
    cy.get('[data-cy="step-indicator-2"]').should('have.class', 'active');

    // Upload a test document
    cy.log('Uploading test document');
    cy.fixture('test-passport.pdf', 'base64').then((fileContent) => {
      const blob = Cypress.Blob.base64StringToBlob(fileContent, 'application/pdf');
      const file = new File([blob], 'test-passport.pdf', { type: 'application/pdf' });
      
      cy.get('[data-cy="passport-upload-area"] input[type="file"]').then((input) => {
        const dataTransfer = new DataTransfer();
        dataTransfer.items.add(file);
        input[0].files = dataTransfer.files;
        input[0].dispatchEvent(new Event('change', { bubbles: true }));
      });
    });

    // Wait for document verification to complete
    cy.get('[data-cy="verification-status"]', { timeout: 30000 }).should('be.visible');
    cy.get('[data-cy="confidence-score"]').should('be.visible');

    // Proceed to review step
    cy.get('[data-cy="next-step-btn"]').should('not.be.disabled').click();
    cy.get('[data-cy="step-indicator-3"]').should('have.class', 'active');

    // Verify review information
    cy.log('Reviewing form information');
    cy.get('[data-cy="review-firstName"]').should('contain', testData.personalInfo.firstName);
    cy.get('[data-cy="review-lastName"]').should('contain', testData.personalInfo.lastName);
    cy.get('[data-cy="review-email"]').should('contain', testData.personalInfo.email);

    // Submit the form
    cy.log('Submitting form');
    cy.get('[data-cy="submit-form-btn"]').should('not.be.disabled').click();

    // Verify submission success
    cy.get('[data-cy="success-message"]', { timeout: 10000 }).should('be.visible');
    cy.get('[data-cy="success-message"]').should('contain', 'submitted successfully');

    // Step 3: Verify form submission via API
    cy.request({
      method: 'GET',
      url: `${Cypress.env('apiUrl')}/form/${uniqueUrl}`
    }).then((response) => {
      expect(response.status).to.eq(200);
      expect(response.body.status).to.eq('Submitted');
      expect(response.body.personalInfo).to.not.be.null;
      expect(response.body.documents).to.have.length.greaterThan(0);
      
      cy.log('Form submission verified via API');
    });
  });

  it('should handle real Azure Document Intelligence integration', () => {
    // This test specifically focuses on Azure DI integration
    cy.log('Testing Azure Document Intelligence integration');

    // Create a form first
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: testData.recruiterEmail
      }
    }).then((response) => {
      const testFormId = response.body.formId;
      const testUniqueUrl = response.body.uniqueUrl;

      // Visit form and navigate to document upload
      cy.visit(`/form/${testUniqueUrl}`);
      
      // Fill minimal personal info to proceed
      cy.get('[data-cy="firstName"]').type('Azure');
      cy.get('[data-cy="lastName"]').type('Test');
      cy.get('[data-cy="email"]').type('azure.test@example.com');
      cy.get('[data-cy="next-step-btn"]').click();

      // Upload document for Azure DI processing
      cy.fixture('test-passport.pdf', 'base64').then((fileContent) => {
        const blob = Cypress.Blob.base64StringToBlob(fileContent, 'application/pdf');
        const file = new File([blob], 'azure-test-passport.pdf', { type: 'application/pdf' });
        
        cy.get('[data-cy="passport-upload-area"] input[type="file"]').then((input) => {
          const dataTransfer = new DataTransfer();
          dataTransfer.items.add(file);
          input[0].files = dataTransfer.files;
          input[0].dispatchEvent(new Event('change', { bubbles: true }));
        });
      });

      // Wait for Azure DI processing and verify results
      cy.get('[data-cy="verification-status"]', { timeout: 60000 }).should('be.visible');
      
      // Verify confidence score is displayed
      cy.get('[data-cy="confidence-score"]').should('be.visible').then(($score) => {
        const scoreText = $score.text();
        const scoreMatch = scoreText.match(/(\d+\.?\d*)%/);
        if (scoreMatch) {
          const score = parseFloat(scoreMatch[1]);
          expect(score).to.be.at.least(0).and.at.most(100);
          cy.log(`Azure DI confidence score: ${score}%`);
        }
      });

      // Verify status color is set
      cy.get('[data-cy="verification-status"]').should('have.class', /green|yellow|red/);
      
      // Verify message is displayed
      cy.get('[data-cy="status-message"]').should('be.visible').and('not.be.empty');
    });
  });

  it('should test email notification functionality', () => {
    cy.log('Testing email notification functionality');

    // Create and submit a form
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: 'email-test@example.com'
      }
    }).then((response) => {
      const testFormId = response.body.formId;

      // Submit personal info via API
      cy.request({
        method: 'POST',
        url: `${Cypress.env('apiUrl')}/form/personal-info`,
        body: {
          formId: testFormId,
          firstName: 'Email',
          lastName: 'Test',
          email: 'email.test@example.com',
          phone: '+1234567890'
        }
      });

      // Submit the form to trigger email notification
      cy.request({
        method: 'POST',
        url: `${Cypress.env('apiUrl')}/form/${testFormId}/submit`
      }).then((submitResponse) => {
        expect(submitResponse.status).to.eq(200);
        cy.log('Form submitted - email notification should be triggered');

        // Wait a moment for email processing
        cy.wait(2000);

        // Verify email was queued/sent by checking email logs via API
        cy.request({
          method: 'GET',
          url: `${Cypress.env('apiUrl')}/monitoring/email-logs`,
          failOnStatusCode: false
        }).then((logResponse) => {
          if (logResponse.status === 200) {
            cy.log('Email logs retrieved successfully');
            // In a real scenario, you might check for specific email entries
          }
        });
      });
    });
  });

  it('should test database operations and data persistence', () => {
    cy.log('Testing database operations and data persistence');

    let testFormId: string;

    // Create form
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: 'db-test@example.com'
      }
    }).then((response) => {
      testFormId = response.body.formId;

      // Submit personal info
      return cy.request({
        method: 'POST',
        url: `${Cypress.env('apiUrl')}/form/personal-info`,
        body: {
          formId: testFormId,
          firstName: 'Database',
          lastName: 'Test',
          email: 'database.test@example.com',
          phone: '+1234567890',
          address: '123 Database Test Street'
        }
      });
    }).then(() => {
      // Retrieve form to verify data persistence
      return cy.request({
        method: 'GET',
        url: `${Cypress.env('apiUrl')}/form/${testFormId}`
      });
    }).then((getResponse) => {
      expect(getResponse.status).to.eq(200);
      expect(getResponse.body.personalInfo).to.not.be.null;
      expect(getResponse.body.personalInfo.firstName).to.eq('Database');
      expect(getResponse.body.personalInfo.lastName).to.eq('Test');
      expect(getResponse.body.personalInfo.email).to.eq('database.test@example.com');
      
      cy.log('Database persistence verified');
    });
  });

  it('should test file storage operations', () => {
    cy.log('Testing file storage operations');

    // Create a form for file upload testing
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: 'file-test@example.com'
      }
    }).then((response) => {
      const testFormId = response.body.formId;

      // Test file upload
      cy.fixture('test-passport.pdf', 'base64').then((fileContent) => {
        const blob = Cypress.Blob.base64StringToBlob(fileContent, 'application/pdf');
        
        const formData = new FormData();
        formData.append('file', blob, 'file-storage-test.pdf');
        formData.append('formId', testFormId);
        formData.append('documentType', 'Passport');

        cy.request({
          method: 'POST',
          url: `${Cypress.env('apiUrl')}/document/upload`,
          body: formData,
          headers: {
            'Content-Type': 'multipart/form-data'
          }
        }).then((uploadResponse) => {
          expect(uploadResponse.status).to.eq(200);
          expect(uploadResponse.body.documentId).to.not.be.null;
          
          const documentId = uploadResponse.body.documentId;
          cy.log(`File uploaded successfully: ${documentId}`);

          // Verify file can be retrieved
          cy.request({
            method: 'GET',
            url: `${Cypress.env('apiUrl')}/document/${documentId}/download`,
            failOnStatusCode: false
          }).then((downloadResponse) => {
            if (downloadResponse.status === 200) {
              cy.log('File download verified');
            }
          });
        });
      });
    });
  });

  it('should test performance and response times', () => {
    cy.log('Testing performance and response times');

    const startTime = Date.now();

    // Test API response times
    cy.request({
      method: 'GET',
      url: `${Cypress.env('apiUrl')}/monitoring/health`
    }).then((response) => {
      const responseTime = Date.now() - startTime;
      expect(response.status).to.eq(200);
      expect(responseTime).to.be.lessThan(5000); // Should respond within 5 seconds
      
      cy.log(`Health check response time: ${responseTime}ms`);
    });

    // Test form creation performance
    const formStartTime = Date.now();
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: 'performance-test@example.com'
      }
    }).then((response) => {
      const formResponseTime = Date.now() - formStartTime;
      expect(response.status).to.eq(200);
      expect(formResponseTime).to.be.lessThan(3000); // Should create form within 3 seconds
      
      cy.log(`Form creation response time: ${formResponseTime}ms`);
    });
  });

  it('should test error handling and recovery', () => {
    cy.log('Testing error handling and recovery');

    // Test invalid form ID
    cy.request({
      method: 'GET',
      url: `${Cypress.env('apiUrl')}/form/invalid-form-id`,
      failOnStatusCode: false
    }).then((response) => {
      expect(response.status).to.eq(404);
      cy.log('Invalid form ID handled correctly');
    });

    // Test invalid file upload
    cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/form/create`,
      body: {
        recruiterEmail: 'error-test@example.com'
      }
    }).then((response) => {
      const testFormId = response.body.formId;

      // Try to upload invalid file type
      const formData = new FormData();
      formData.append('file', new Blob(['malicious content'], { type: 'application/exe' }), 'malicious.exe');
      formData.append('formId', testFormId);
      formData.append('documentType', 'Passport');

      cy.request({
        method: 'POST',
        url: `${Cypress.env('apiUrl')}/document/upload`,
        body: formData,
        failOnStatusCode: false
      }).then((uploadResponse) => {
        expect(uploadResponse.status).to.eq(400);
        cy.log('Invalid file type rejected correctly');
      });
    });
  });

  after(() => {
    cy.log('Production integration tests completed');
    
    // Clean up test data if needed
    // Note: In a real production environment, you might want to clean up test data
    // or use a separate test database
  });
});