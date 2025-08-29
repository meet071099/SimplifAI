import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PaginatedFormsResponse {
  forms: FormSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FormSummary {
  id: string;
  candidateName: string;
  candidateEmail: string;
  status: string;
  submittedAt?: Date;
  createdAt: Date;
  documentCount: number;
  hasLowConfidenceDocuments: boolean;
  recruiterEmail: string;
}

export interface FormReview {
  id: string;
  status: string;
  createdAt: Date;
  submittedAt?: Date;
  recruiterEmail: string;
  personalInfo?: PersonalInfo;
  documents: DocumentReview[];
}

export interface PersonalInfo {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  dateOfBirth?: Date;
  createdAt: Date;
}

export interface DocumentReview {
  id: string;
  documentType: string;
  fileName: string;
  fileSize: number;
  contentType: string;
  uploadedAt: Date;
  verificationStatus: string;
  confidenceScore?: number;
  isBlurred: boolean;
  isCorrectType: boolean;
  statusColor: string;
  verificationDetails?: string;
  filePath: string;
}

export interface DashboardStats {
  totalForms: number;
  submittedForms: number;
  approvedForms: number;
  rejectedForms: number;
  pendingReview: number;
  formsWithLowConfidenceDocuments: number;
}

export interface FormStatusUpdateRequest {
  status: string;
  reviewNotes?: string;
}

export interface FormStatusUpdateResponse {
  formId: string;
  status: string;
  reviewNotes?: string;
  updatedAt: Date;
}

@Injectable({
  providedIn: 'root'
})
export class RecruiterService {
  private readonly baseUrl = `${environment.apiUrl}/api/recruiter`;

  constructor(private http: HttpClient) {}

  /**
   * Gets submitted forms with filtering and pagination
   */
  async getSubmittedForms(
    recruiterEmail?: string,
    status?: string,
    searchTerm?: string,
    page: number = 1,
    pageSize: number = 10
  ): Promise<PaginatedFormsResponse> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (recruiterEmail) {
      params = params.set('recruiterEmail', recruiterEmail);
    }
    if (status) {
      params = params.set('status', status);
    }
    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }

    try {
      const response = await firstValueFrom(
        this.http.get<PaginatedFormsResponse>(`${this.baseUrl}/forms`, { params })
      );
      
      // Convert date strings to Date objects
      response.forms = response.forms.map(form => ({
        ...form,
        createdAt: new Date(form.createdAt),
        submittedAt: form.submittedAt ? new Date(form.submittedAt) : undefined
      }));

      return response;
    } catch (error) {
      console.error('Error fetching submitted forms:', error);
      throw error;
    }
  }

  /**
   * Gets detailed form information for review
   */
  async getFormForReview(formId: string): Promise<FormReview> {
    try {
      const response = await firstValueFrom(
        this.http.get<FormReview>(`${this.baseUrl}/forms/${formId}`)
      );

      // Convert date strings to Date objects
      const form: FormReview = {
        ...response,
        createdAt: new Date(response.createdAt),
        submittedAt: response.submittedAt ? new Date(response.submittedAt) : undefined,
        personalInfo: response.personalInfo ? {
          ...response.personalInfo,
          createdAt: new Date(response.personalInfo.createdAt),
          dateOfBirth: response.personalInfo.dateOfBirth ? new Date(response.personalInfo.dateOfBirth) : undefined
        } : undefined,
        documents: response.documents.map(doc => ({
          ...doc,
          uploadedAt: new Date(doc.uploadedAt)
        }))
      };

      return form;
    } catch (error) {
      console.error('Error fetching form for review:', error);
      throw error;
    }
  }

  /**
   * Updates form status (approve/reject)
   */
  async updateFormStatus(
    formId: string, 
    status: string, 
    reviewNotes?: string
  ): Promise<FormStatusUpdateResponse> {
    const request: FormStatusUpdateRequest = {
      status,
      reviewNotes
    };

    try {
      const response = await firstValueFrom(
        this.http.put<FormStatusUpdateResponse>(`${this.baseUrl}/forms/${formId}/status`, request)
      );

      return {
        ...response,
        updatedAt: new Date(response.updatedAt)
      };
    } catch (error) {
      console.error('Error updating form status:', error);
      throw error;
    }
  }

  /**
   * Gets dashboard statistics
   */
  async getDashboardStats(recruiterEmail?: string): Promise<DashboardStats> {
    let params = new HttpParams();
    
    if (recruiterEmail) {
      params = params.set('recruiterEmail', recruiterEmail);
    }

    try {
      return await firstValueFrom(
        this.http.get<DashboardStats>(`${this.baseUrl}/dashboard/stats`, { params })
      );
    } catch (error) {
      console.error('Error fetching dashboard stats:', error);
      throw error;
    }
  }

  /**
   * Downloads a document file
   */
  async downloadDocument(documentId: string): Promise<Blob> {
    try {
      return await firstValueFrom(
        this.http.get(`${this.baseUrl}/documents/${documentId}/download`, {
          responseType: 'blob'
        })
      );
    } catch (error) {
      console.error('Error downloading document:', error);
      throw error;
    }
  }

  /**
   * Gets document file URL for viewing
   */
  getDocumentViewUrl(documentId: string): string {
    return `${this.baseUrl}/documents/${documentId}/view`;
  }
}