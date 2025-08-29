// This file can be replaced during build by using the `fileReplacements` array.
// `ng build` replaces `environment.ts` with `environment.prod.ts`.
// The list of file replacements can be found in `angular.json`.

export const environment = {
  production: false,
  apiUrl: 'https://localhost:7292',
  demoMode: false, // Enable demo mode for development without backend

  // Error handling configuration
  errorHandling: {
    enableGlobalErrorHandler: true,
    enableHttpErrorInterceptor: true,
    showDetailedErrors: true, // Show detailed errors in development
    retryAttempts: 3,
    retryDelay: 1000,
    enableErrorReporting: false // Disable error reporting in development
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

/*
 * For easier debugging in development mode, you can import the following file
 * to ignore zone related error stack frames such as `zone.run`, `zoneDelegate.invokeTask`.
 *
 * This import should be commented out in production mode because it will have a negative impact
 * on performance if an error is thrown.
 */
// import 'zone.js/plugins/zone-error';  // Included with Angular CLI.
