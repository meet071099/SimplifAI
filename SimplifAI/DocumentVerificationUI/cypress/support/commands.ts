/// <reference types="cypress" />

// Custom command to select elements by data-cy attribute
Cypress.Commands.add('dataCy', (value: string) => {
  return cy.get(`[data-cy="${value}"]`);
});

// Custom command to fill personal information form
Cypress.Commands.add('fillPersonalInfo', (personalInfo: any) => {
  cy.dataCy('firstName').clear().type(personalInfo.firstName);
  cy.dataCy('lastName').clear().type(personalInfo.lastName);
  cy.dataCy('email').clear().type(personalInfo.email);
  cy.dataCy('phone').clear().type(personalInfo.phone);
  if (personalInfo.address) {
    cy.dataCy('address').clear().type(personalInfo.address);
  }
  if (personalInfo.dateOfBirth) {
    cy.dataCy('dateOfBirth').clear().type(personalInfo.dateOfBirth);
  }
});

// Custom command to upload a file
Cypress.Commands.add('uploadFile', (fileName: string, mimeType: string = 'image/jpeg') => {
  cy.fixture(fileName, 'base64').then((fileContent: any) => {
    const blob = Cypress.Blob.base64StringToBlob(fileContent, mimeType);
    const file = new File([blob], fileName, { type: mimeType });
    
    cy.get('input[type="file"]').then((input: any) => {
      const dataTransfer = new DataTransfer();
      dataTransfer.items.add(file);
      input[0].files = dataTransfer.files;
      input[0].dispatchEvent(new Event('change', { bubbles: true }));
    });
  });
});

// Custom command to wait for API response
Cypress.Commands.add('waitForApi', (alias: string, timeout: number = 10000) => {
  return cy.wait(alias, { timeout });
});

// Custom command to check API health
Cypress.Commands.add('checkApiHealth', () => {
  return cy.request({
    method: 'GET',
    url: `${Cypress.env('apiUrl')}/monitoring/health`,
    failOnStatusCode: false
  }).then((response) => {
    expect(response.status).to.eq(200);
  });
});

// Custom command to create a test form via API
Cypress.Commands.add('createTestForm', (recruiterEmail: string = 'test@example.com') => {
  return cy.request({
    method: 'POST',
    url: `${Cypress.env('apiUrl')}/form/create`,
    body: {
      recruiterEmail: recruiterEmail
    }
  }).then((response) => {
    expect(response.status).to.eq(200);
    return {
      formId: response.body.formId,
      uniqueUrl: response.body.uniqueUrl
    };
  });
});

// Custom command to submit personal info via API
Cypress.Commands.add('submitPersonalInfoApi', (formId: string, personalInfo: any) => {
  return cy.request({
    method: 'POST',
    url: `${Cypress.env('apiUrl')}/form/personal-info`,
    body: {
      formId: formId,
      ...personalInfo
    }
  });
});

// Custom command to upload document via API
Cypress.Commands.add('uploadDocumentApi', (formId: string, fileName: string, documentType: string = 'Passport') => {
  return cy.fixture(fileName, 'base64').then((fileContent: any) => {
    const blob = Cypress.Blob.base64StringToBlob(fileContent, 'application/pdf');
    
    const formData = new FormData();
    formData.append('file', blob, fileName);
    formData.append('formId', formId);
    formData.append('documentType', documentType);

    return cy.request({
      method: 'POST',
      url: `${Cypress.env('apiUrl')}/document/upload`,
      body: formData
    });
  });
});

// Custom command to verify confidence score display
Cypress.Commands.add('verifyConfidenceScore', (expectedRange?: { min: number, max: number }) => {
  cy.dataCy('confidence-score').should('be.visible').then(($score) => {
    const scoreText = $score.text();
    const scoreMatch = scoreText.match(/(\d+\.?\d*)%/);
    
    if (scoreMatch) {
      const score = parseFloat(scoreMatch[1]);
      expect(score).to.be.at.least(0).and.at.most(100);
      
      if (expectedRange) {
        expect(score).to.be.at.least(expectedRange.min).and.at.most(expectedRange.max);
      }
    }
  });
});

// Custom command to verify document status color
Cypress.Commands.add('verifyStatusColor', (expectedColor: 'green' | 'yellow' | 'red') => {
  cy.dataCy('verification-status').should('have.class', expectedColor);
});

declare global {
  namespace Cypress {
    interface Chainable {
      dataCy(value: string): Chainable<JQuery<HTMLElement>>
      fillPersonalInfo(personalInfo: any): Chainable<void>
      uploadFile(fileName: string, mimeType?: string): Chainable<void>
      waitForApi(alias: string, timeout?: number): Chainable<any>
      checkApiHealth(): Chainable<any>
      createTestForm(recruiterEmail?: string): Chainable<{ formId: string, uniqueUrl: string }>
      submitPersonalInfoApi(formId: string, personalInfo: any): Chainable<any>
      uploadDocumentApi(formId: string, fileName: string, documentType?: string): Chainable<any>
      verifyConfidenceScore(expectedRange?: { min: number, max: number }): Chainable<void>
      verifyStatusColor(expectedColor: 'green' | 'yellow' | 'red'): Chainable<void>
    }
  }
}