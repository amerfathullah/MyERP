import { CanDeactivateFn } from '@angular/router';
import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';

/**
 * Interface for components that track unsaved changes.
 * Implement this on form components to enable the unsaved-changes guard.
 */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

/**
 * Route guard that warns users before navigating away from a form with unsaved changes.
 * Usage in routes: `canDeactivate: [unsavedChangesGuard]`
 */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component): Observable<boolean> | boolean => {
  if (!component.hasUnsavedChanges || !component.hasUnsavedChanges()) {
    return true;
  }

  const confirmation = inject(ConfirmationService);
  return confirmation.warn(
    'You have unsaved changes. Are you sure you want to leave?',
    'Unsaved Changes'
  ).pipe(
    map(status => status === Confirmation.Status.confirm)
  );
};
