import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

/**
 * Global HTTP error interceptor that shows business exception messages as toast notifications.
 * ABP business exceptions return structured error objects:
 * { error: { code: "MyERP:XXXXX", message: "...", details: "..." } }
 *
 * This interceptor catches 400/403/422/500 responses and shows the message.
 * Auth (401) and not-found (404) are handled by ABP's built-in interceptors.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toaster = inject(ToasterService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 || error.status === 404) {
        // Let ABP handle auth and not-found
        return throwError(() => error);
      }

      const abpError = error.error?.error;
      if (abpError?.message) {
        // ABP business exception with structured message
        toaster.error(abpError.message, abpError.code ?? 'Error');
      } else if (error.status === 403) {
        toaster.warn('You do not have permission to perform this action.', 'Access Denied');
      } else if (error.status === 429) {
        toaster.warn('Too many requests. Please wait a moment.', 'Rate Limited');
      } else if (error.status >= 500) {
        toaster.error('An unexpected error occurred. Please try again.', 'Server Error');
      }

      return throwError(() => error);
    })
  );
};
