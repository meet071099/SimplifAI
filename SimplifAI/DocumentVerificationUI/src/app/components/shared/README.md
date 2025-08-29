# Shared UI Components

This directory contains reusable UI components for the Document Verification System.

## Components

### 1. FileUploadComponent (`app-file-upload`)

A drag-and-drop file upload component with progress indication and file preview.

**Features:**
- Drag and drop file upload
- File type and size validation
- Upload progress bar with animation
- Image preview for image files
- File information display
- Remove file functionality

**Usage:**
```html
<app-file-upload
  [documentType]="'Passport'"
  [acceptedTypes]="'.jpg,.jpeg,.png,.pdf'"
  [maxSizeInMB]="10"
  [disabled]="false"
  [currentFile]="existingFile"
  (fileSelected)="onFileSelected($event)"
  (fileRemoved)="onFileRemoved($event)">
</app-file-upload>
```

**Events:**
- `fileSelected`: Emitted when a file is selected (FileUploadEvent)
- `fileRemoved`: Emitted when a file is removed (string - documentType)

### 2. ConfidenceScoreComponent (`app-confidence-score`)

Displays confidence scores with color-coded visual indicators.

**Features:**
- Color-coded confidence levels (Green/Yellow/Red)
- Visual tick indicators
- Percentage display
- Multiple sizes (small/medium/large)
- Animated entrance
- Progress bar for large size

**Usage:**
```html
<app-confidence-score
  [score]="85"
  [level]="'green'"
  [showPercentage]="true"
  [size]="'medium'"
  [animated]="true">
</app-confidence-score>
```

**Confidence Levels:**
- Green (85-100%): High confidence, verified
- Yellow (50-84%): Medium confidence, review required
- Red (0-49%): Low confidence, failed

### 3. DocumentVerificationComponent (`app-document-verification`)

Displays document verification status with confidence scores and action buttons.

**Features:**
- Document information display
- Confidence score integration
- Verification status messages
- Action buttons (Confirm/Replace/Remove)
- Processing state indication

**Usage:**
```html
<app-document-verification
  [document]="documentInfo"
  [showActions]="true"
  [isProcessing]="false"
  (documentAction)="onDocumentAction($event)">
</app-document-verification>
```

**Events:**
- `documentAction`: Emitted when an action button is clicked (DocumentAction)

### 4. AlertComponent (`app-alert`)

Flexible alert component for displaying messages, warnings, and confirmations.

**Features:**
- Multiple alert types (success/warning/error/info)
- Dismissible alerts
- Action buttons
- Custom content slots
- Animated entrance/exit
- Auto-dismiss for success messages

**Usage:**
```html
<app-alert
  type="success"
  title="Success!"
  message="Document uploaded successfully"
  [dismissible]="true"
  [actions]="alertActions"
  (dismissed)="onAlertDismissed()"
  (actionClicked)="onAlertAction($event)">
  
  <!-- Custom content can be added here -->
  <p>Additional custom content</p>
</app-alert>
```

**Alert Types:**
- `success`: Green, for successful operations
- `warning`: Yellow, for warnings and cautions
- `error`: Red, for errors and failures
- `info`: Blue, for informational messages

### 5. LoadingSpinnerComponent (`app-loading-spinner`)

Animated loading spinner with customizable appearance and overlay support.

**Features:**
- Multiple sizes (small/medium/large)
- Color variants (primary/secondary/success/warning/danger)
- Overlay mode for full-screen loading
- Customizable loading message
- Smooth animations

**Usage:**
```html
<app-loading-spinner
  [size]="'medium'"
  [message]="'Loading...'"
  [showMessage]="true"
  [color]="'primary'"
  [overlay]="false"
  [centered]="true">
</app-loading-spinner>
```

## Integration Example

Here's how these components work together in the document upload workflow:

```html
<!-- File Upload -->
<app-file-upload
  [documentType]="'Passport'"
  [acceptedTypes]="'.jpg,.jpeg,.png,.pdf'"
  [maxSizeInMB]="10"
  (fileSelected)="onFileUploaded($event)">
</app-file-upload>

<!-- Document Verification (shown after upload) -->
<app-document-verification
  [document]="uploadedDocument"
  [isProcessing]="isVerifying"
  (documentAction)="onDocumentAction($event)">
</app-document-verification>

<!-- Alert for user feedback -->
<app-alert
  *ngIf="alertMessage"
  [type]="alertType"
  [message]="alertMessage"
  (dismissed)="clearAlert()">
</app-alert>

<!-- Loading overlay during processing -->
<app-loading-spinner
  *ngIf="isProcessing"
  [overlay]="true"
  [message]="'Processing document...'"
  [size]="'large'">
</app-loading-spinner>
```

## Styling

All components follow a consistent design system with:
- Responsive design for mobile and desktop
- Smooth animations and transitions
- Accessible color contrasts
- Hover and focus states
- Modern, clean aesthetics

## Requirements Covered

These components fulfill the following requirements:
- **2.5, 2.6**: File upload with drag-drop and progress indication
- **4.1-4.9**: Document verification with confidence scores and visual feedback
- **7.1-7.5**: Responsive design with animations and modern UI