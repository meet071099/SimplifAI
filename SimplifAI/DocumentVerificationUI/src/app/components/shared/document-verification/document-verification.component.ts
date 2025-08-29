import { Component, Input, Output, EventEmitter } from '@angular/core';
import { DocumentInfo } from '../../../models/form.models';

export interface DocumentAction {
  action: 'confirm' | 'replace' | 'remove';
  documentType: string;
}

@Component({
  selector: 'app-document-verification',
  templateUrl: './document-verification.component.html',
  styleUrls: ['./document-verification.component.css']
})
export class DocumentVerificationComponent {
  @Input() document: DocumentInfo | null = null;
  @Input() showActions: boolean = true;
  @Input() isProcessing: boolean = false;
  
  @Output() documentAction = new EventEmitter<DocumentAction>();
  
  get verificationMessage(): string {
    if (!this.document) return '';
    
    if (this.document.isBlurred) {
      return 'Document appears blurred or unclear. Please upload a clearer image.';
    }
    
    if (!this.document.isCorrectType) {
      return `Document type mismatch. Expected ${this.document.documentType}, but detected a different type.`;
    }

    if(this.document.message){
      return this.document.message;
    }

    // Handle authenticity verification results with detailed reasons
    if (this.document.authenticityResult && this.document.aiFoundryUsed) {
      if (this.document.authenticityVerified) {
        return `Document verified successfully. Personal information matches form data with ${this.document.confidenceScore}% confidence.`;
      } else {
        // Use the detailed message from the API response if available
        if (this.document.message && this.document.message !== 'Document verification completed with low confidence. Please confirm if this is correct.') {
          const confidence = this.document.confidenceScore || 0;
          return `Document has low confidence (${confidence}%) - ${this.document.message}`;
        } else if (this.document.authenticityReason) {
          // Fallback to the old authenticityReason field
          const reason = this.document.authenticityReason;
          const confidence = this.document.confidenceScore || 0;
          
          if (reason.toLowerCase().includes('name mismatch')) {
            return `Document has low confidence (${confidence}%) due to name mismatch. ${reason}`;
          } else if (reason.toLowerCase().includes('wrong document type') || reason.toLowerCase().includes('document type')) {
            return `Document has low confidence (${confidence}%) due to document type mismatch. ${reason}`;
          } else if (reason.toLowerCase().includes('fake') || reason.toLowerCase().includes('tampered') || reason.toLowerCase().includes('fraud')) {
            return `Document has low confidence (${confidence}%) due to suspected fraud. ${reason}`;
          } else if (reason.toLowerCase().includes('not found') || reason.toLowerCase().includes('unclear') || reason.toLowerCase().includes('incomplete')) {
            return `Document has low confidence (${confidence}%) due to unclear information. ${reason}`;
          } else {
            return `Document has low confidence (${confidence}%) due to verification failure. ${reason}`;
          }
        } else if (this.document.authenticityResult.startsWith('not authentic')) {
          return `Document has low confidence (${this.document.confidenceScore || 0}%) due to authenticity verification failure. Personal information does not match form data.`;
        }
      }
    }
    
    // Handle cases where AI Foundry wasn't used but we have low confidence
    if (this.document.confidenceScore !== undefined) {
      if (this.document.confidenceScore >= 85) {
        return 'Document verification successful with high confidence.';
      } else if (this.document.confidenceScore >= 50) {
        return `Document has moderate confidence (${this.document.confidenceScore}%) and requires manual review. Please confirm if you want to proceed or upload a different document.`;
      } else {
        return `Document has low confidence (${this.document.confidenceScore}%) due to poor image quality or unclear text. Please confirm if you want to proceed or upload a clearer document.`;
      }
    }
    
    return this.document.message || 'Document verification in progress...';
  }
  
  get showConfirmButton(): boolean {
    return this.document?.confidenceScore !== undefined && 
           this.document.confidenceScore < 85 && 
           !this.document.isBlurred && 
           this.document.isCorrectType !== false;
  }
  
  get showReplaceButton(): boolean {
    return this.document?.verificationStatus === 'failed' || 
           this.document?.isBlurred === true || 
           this.document?.isCorrectType === false;
  }
  
  onConfirm() {
    if (this.document) {
      this.documentAction.emit({
        action: 'confirm',
        documentType: this.document.documentType
      });
    }
  }
  
  onReplace() {
    if (this.document) {
      this.documentAction.emit({
        action: 'replace',
        documentType: this.document.documentType
      });
    }
  }
  
  onRemove() {
    if (this.document) {
      this.documentAction.emit({
        action: 'remove',
        documentType: this.document.documentType
      });
    }
  }

  getFailureType(): string {
    if (!this.document?.authenticityReason) return 'general';
    
    const reason = this.document.authenticityReason.toLowerCase();
    
    if (reason.includes('name mismatch')) {
      return 'name_mismatch';
    } else if (reason.includes('wrong document type') || reason.includes('document type')) {
      return 'document_type';
    } else if (reason.includes('fake') || reason.includes('tampered') || reason.includes('fraud')) {
      return 'fraud';
    } else if (reason.includes('not found') || reason.includes('unclear') || reason.includes('incomplete')) {
      return 'unclear';
    } else {
      return 'general';
    }
  }
}