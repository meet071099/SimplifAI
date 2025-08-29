# Recruiter Dashboard

The Recruiter Dashboard provides a comprehensive interface for recruiters to review and manage submitted candidate forms.

## Features

### Dashboard Overview
- **Statistics Cards**: Display key metrics including total forms, submitted forms, pending reviews, approved/rejected forms, and forms with low confidence documents
- **Real-time Updates**: Dashboard statistics update automatically when forms are processed
- **Visual Indicators**: Color-coded status badges and warning indicators for forms requiring attention

### Form Management
- **Form List**: Paginated list of all submitted forms with key information
- **Search & Filter**: 
  - Search by candidate name or email
  - Filter by form status (Submitted, Under Review, Approved, Rejected)
- **Sorting**: Forms sorted by submission date (most recent first)

### Form Review Interface
- **Detailed View**: Complete form information including personal details and document verification results
- **Document Viewer**: View uploaded documents with verification status and confidence scores
- **Status Management**: Update form status with optional review notes
- **Confidence Score Display**: Visual indicators for document verification confidence levels

## Components

### RecruiterDashboardComponent
Main dashboard component that displays:
- Dashboard statistics
- Form search and filtering
- Paginated form list
- Navigation to detailed form review

### FormReviewComponent
Detailed form review interface that provides:
- Complete candidate information
- Document verification details
- Status update functionality
- Document viewing capabilities

## Services

### RecruiterService
Handles all API communication for recruiter functionality:
- `getSubmittedForms()`: Retrieve paginated list of submitted forms
- `getFormForReview()`: Get detailed form information
- `updateFormStatus()`: Update form approval/rejection status
- `getDashboardStats()`: Retrieve dashboard statistics

## Navigation

The dashboard is accessible via:
- Direct URL: `/recruiter/dashboard`
- Navigation link in the main header
- Individual form review: `/recruiter/forms/:id`

## Status Workflow

Forms progress through the following statuses:
1. **Submitted**: Initial status when candidate submits form
2. **Under Review**: Recruiter is actively reviewing the form
3. **Approved**: Form has been approved by recruiter
4. **Rejected**: Form has been rejected by recruiter

## Visual Indicators

### Status Colors
- **Blue**: Submitted forms
- **Orange**: Under Review
- **Green**: Approved
- **Red**: Rejected

### Confidence Scores
- **Green (✅)**: High confidence (85%+)
- **Yellow (⚠️)**: Medium confidence (50-84%)
- **Red (❌)**: Low confidence (<50%)

## Responsive Design

The dashboard is fully responsive and works on:
- Desktop computers
- Tablets
- Mobile devices

## Accessibility

The interface includes:
- Keyboard navigation support
- Screen reader compatibility
- High contrast mode support
- Touch-friendly interactions on mobile devices