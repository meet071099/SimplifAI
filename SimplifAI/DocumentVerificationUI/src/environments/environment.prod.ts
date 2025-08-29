export const environment = {
  production: true,
  apiUrl: 'https://localhost:7000/api',
  demoMode: false, // Disable demo mode in production
  
  // Error handling configuration
  errorHandling: {
    enableGlobalErrorHandler: true,
    enableHttpErrorInterceptor: true,
    showDetailedErrors: false, // Hide detailed errors in production
    retryAttempts: 3,
    retryDelay: 1000,
    enableErrorReporting: true // Enable error reporting in production
  },
  
  // File validation configuration
  fileValidation: {
    maxSizeInMB: 10,
    minSizeInKB: 1,
    allowedTypes: ['image/jpeg', 'image/jpg', 'image/png', 'application/pdf'],
    allowedExtensions: ['.jpg', '.jpeg', '.png', '.pdf']
  },
  
  // Notification configuration
  notifications: {
    defaultDuration: 5000,
    errorDuration: 0, // Persistent error notifications
    warningDuration: 8000,
    successDuration: 3000
  }
};
