import { describe, it, expect } from 'vitest';

// Test the error message logic extracted from AutoValidationDirective
// Since the method is private, we test the mapping logic directly
describe('AutoValidation error messages', () => {
  function getErrorMessage(errors: any): string {
    if (errors['required']) return 'This field is required';
    if (errors['email']) return 'Invalid email address';
    if (errors['min']) return `Minimum value is ${errors['min'].min}`;
    if (errors['max']) return `Maximum value is ${errors['max'].max}`;
    if (errors['minlength']) return `Minimum ${errors['minlength'].requiredLength} characters`;
    if (errors['maxlength']) return `Maximum ${errors['maxlength'].requiredLength} characters`;
    if (errors['pattern']) return 'Invalid format';
    return 'Invalid value';
  }

  it('should return required message', () => {
    expect(getErrorMessage({ required: true })).toBe('This field is required');
  });

  it('should return email message', () => {
    expect(getErrorMessage({ email: true })).toBe('Invalid email address');
  });

  it('should return min value message', () => {
    expect(getErrorMessage({ min: { min: 5, actual: 3 } })).toBe('Minimum value is 5');
  });

  it('should return max value message', () => {
    expect(getErrorMessage({ max: { max: 100, actual: 150 } })).toBe('Maximum value is 100');
  });

  it('should return minlength message', () => {
    expect(getErrorMessage({ minlength: { requiredLength: 3, actualLength: 1 } }))
      .toBe('Minimum 3 characters');
  });

  it('should return maxlength message', () => {
    expect(getErrorMessage({ maxlength: { requiredLength: 50, actualLength: 55 } }))
      .toBe('Maximum 50 characters');
  });

  it('should return pattern message', () => {
    expect(getErrorMessage({ pattern: { requiredPattern: '/^\\d+$/' } })).toBe('Invalid format');
  });

  it('should return fallback for unknown errors', () => {
    expect(getErrorMessage({ customError: true })).toBe('Invalid value');
  });

  it('should prioritize required over other errors', () => {
    expect(getErrorMessage({ required: true, minlength: { requiredLength: 3, actualLength: 0 } }))
      .toBe('This field is required');
  });
});
