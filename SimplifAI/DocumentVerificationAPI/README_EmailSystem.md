# Email Notification System

## Overview

The Document Verification System includes a robust email notification system with queue-based delivery, retry logic, and comprehensive logging. This system ensures reliable delivery of form submission notifications to recruiters.

## Features

### 1. Email Templates
- **Form Submission Notifications**: HTML-formatted emails sent to recruiters when candidates submit forms
- **Responsive Design**: Email templates work well across different email clients
- **Rich Content**: Includes personal information, document status, and confidence scores

### 2. SMTP Configuration
- **Mailtrap Integration**: Configured to use Mailtrap for development and testing
- **Configurable Settings**: SMTP settings can be modified in `appsettings.json`
- **SSL/TLS Support**: Secure email transmission

### 3. Email Queue System
- **Reliable Delivery**: Emails are queued for processing to handle failures gracefully
- **Priority System**: High priority for form submissions, normal/low priority for other emails
- **Batch Processing**: Processes multiple emails in batches for efficiency

### 4. Retry Logic
- **Exponential Backoff**: Failed emails are retried with increasing delays (5, 10, 20 minutes)
- **Maximum Retries**: Configurable maximum retry attempts (default: 3)
- **Automatic Retry**: Background service automatically processes failed emails

### 5. Comprehensive Logging
- **Email Logs**: Detailed logs for each email attempt with timing and error information
- **Queue Statistics**: Track pending, sent, failed, and retry email counts
- **Performance Monitoring**: Duration tracking for email sending operations

## Configuration

### SMTP Settings (appsettings.json)
```json
{
  "Email": {
    "SmtpHost": "sandbox.smtp.mailtrap.io",
    "SmtpPort": 587,
    "Username": "1e0ced1c7ae6ed",
    "Password": "0ade4f0af5136b",
    "FromEmail": "noreply@documentverification.com",
    "ProcessingIntervalMinutes": 2
  }
}
```

### Database Tables

#### EmailQueue
- Stores emails pending delivery
- Tracks retry attempts and status
- Links to forms for context

#### EmailLogs
- Records all email sending attempts
- Stores error details and performance metrics
- Provides audit trail for troubleshooting

## API Endpoints

### Email Management
- `GET /api/email/queue/stats` - Get email queue statistics
- `POST /api/email/queue/process` - Manually process email queue
- `POST /api/email/test` - Test email service configuration
- `POST /api/email/send-test` - Send a test email

## Usage

### Automatic Email Sending
When a form is submitted, the system automatically:
1. Queues a form submission notification email
2. Background service processes the queue every 1-2 minutes
3. Sends email with retry logic if failures occur
4. Logs all attempts for monitoring

### Manual Processing
Administrators can manually trigger email processing:
```bash
curl -X POST "https://localhost:7001/api/email/queue/process?batchSize=10"
```

### Monitoring
Check email queue status:
```bash
curl -X GET "https://localhost:7001/api/email/queue/stats"
```

## Email Template Structure

### Form Submission Notification
- **Header**: Form submission confirmation with timestamp
- **Personal Information**: Candidate details (name, email, phone, address, DOB)
- **Document Status**: Each uploaded document with:
  - Document type and filename
  - Verification status with color coding
  - Confidence score percentage
  - Upload timestamp
- **Footer**: Instructions for recruiter next steps

### Status Color Coding
- **Green**: High confidence (85%+) - Document verified successfully
- **Yellow**: Medium confidence (50-85%) - Requires review
- **Red**: Low confidence (<50%) or failed verification - Requires attention

## Error Handling

### SMTP Errors
- Connection failures are logged with detailed error messages
- Authentication errors are captured and reported
- Network timeouts trigger retry logic

### Database Errors
- Queue operations are wrapped in transactions
- Failed database operations are logged and retried
- Data integrity is maintained during failures

### Service Failures
- Background service continues running even if individual emails fail
- Service restart automatically resumes queue processing
- Failed emails remain in queue for manual intervention if needed

## Performance Considerations

### Batch Processing
- Processes up to 10 emails per batch by default
- Configurable batch size for different load requirements
- Prevents overwhelming SMTP servers

### Background Processing
- Non-blocking email sending doesn't delay form submissions
- Separate background service handles email queue
- Configurable processing intervals

### Database Optimization
- Indexed queries for efficient queue processing
- Cleanup procedures for old email logs (can be implemented)
- Optimized queries for statistics and monitoring

## Troubleshooting

### Common Issues

1. **Emails Not Sending**
   - Check SMTP configuration in appsettings.json
   - Verify network connectivity to SMTP server
   - Check email queue stats for failed emails

2. **High Retry Count**
   - Review SMTP server logs
   - Check for authentication issues
   - Verify email addresses are valid

3. **Queue Backup**
   - Monitor queue statistics regularly
   - Increase processing frequency if needed
   - Check for persistent SMTP issues

### Monitoring Commands
```bash
# Check queue status
curl -X GET "https://localhost:7001/api/email/queue/stats"

# Test email service
curl -X POST "https://localhost:7001/api/email/test"

# Process queue manually
curl -X POST "https://localhost:7001/api/email/queue/process"
```

## Future Enhancements

### Potential Improvements
1. **Email Templates**: More template types for different notifications
2. **Scheduling**: Delayed email sending for specific times
3. **Attachments**: Support for document attachments in emails
4. **Analytics**: Email open/click tracking
5. **Cleanup**: Automatic cleanup of old email logs
6. **Webhooks**: Integration with external notification systems

### Scalability
1. **Multiple Workers**: Multiple background services for high volume
2. **External Queue**: Redis or Azure Service Bus for distributed systems
3. **Load Balancing**: Multiple SMTP servers for redundancy
4. **Monitoring**: Integration with application monitoring tools