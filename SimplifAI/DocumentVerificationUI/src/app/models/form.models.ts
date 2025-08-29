export interface PersonalInfo {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  dateOfBirth: Date | null;
}

export interface DocumentInfo {
  id?: string;
  documentType: string;
  fileName: string;
  file?: File;
  verificationStatus?: 'pending' | 'verified' | 'failed';
  confidenceScore?: number;
  statusColor?: 'green' | 'yellow' | 'red';
  isBlurred?: boolean;
  isCorrectType?: boolean;
  message?: string;
  // Authenticity verification fields
  authenticityResult?: string;
  authenticityVerified?: boolean;
  authenticityReason?: string; // Detailed reason from JSON response
  authenticityExplanation?: string; // Detailed explanation from AI
  authenticityRawResponse?: string; // Complete raw response from AI agent
  aiFoundryUsed?: boolean;
}

export interface FormData {
  id?: string;
  personalInfo: PersonalInfo;
  documents: DocumentInfo[];
  status: 'draft' | 'submitted' | 'completed';
  createdAt?: Date;
  submittedAt?: Date;
}

export interface StepValidation {
  isValid: boolean;
  errors: string[];
}

export const REQUIRED_DOCUMENT_TYPES = [
  'Passport',
  'Driver License',
  'National ID',
  'Birth Certificate'
];