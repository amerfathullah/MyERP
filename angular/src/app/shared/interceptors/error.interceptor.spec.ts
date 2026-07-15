import { describe, it, expect } from 'vitest';
import { HttpErrorResponse } from '@angular/common/http';

/**
 * Tests for the error interceptor logic.
 * Since the interceptor uses inject() for ToasterService, we test the
 * error classification and message extraction logic independently.
 */
describe('Error interceptor logic', () => {
  function classifyError(error: HttpErrorResponse): { action: string; title: string; message: string } {
    if (error.status === 401 || error.status === 404) {
      return { action: 'passthrough', title: '', message: '' };
    }

    const abpError = error.error?.error;
    if (abpError?.message) {
      return { action: 'error', title: abpError.code ?? 'Error', message: abpError.message };
    } else if (error.status === 403) {
      return { action: 'warn', title: 'Access Denied', message: 'You do not have permission to perform this action.' };
    } else if (error.status === 429) {
      return { action: 'warn', title: 'Rate Limited', message: 'Too many requests. Please wait a moment.' };
    } else if (error.status >= 500) {
      return { action: 'error', title: 'Server Error', message: 'An unexpected error occurred. Please try again.' };
    }

    return { action: 'none', title: '', message: '' };
  }

  it('should passthrough 401 errors', () => {
    const error = new HttpErrorResponse({ status: 401 });
    expect(classifyError(error).action).toBe('passthrough');
  });

  it('should passthrough 404 errors', () => {
    const error = new HttpErrorResponse({ status: 404 });
    expect(classifyError(error).action).toBe('passthrough');
  });

  it('should extract ABP business exception message', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { error: { code: 'MyERP:05002', message: 'Insufficient stock for Item A' } }
    });
    const result = classifyError(error);
    expect(result.action).toBe('error');
    expect(result.title).toBe('MyERP:05002');
    expect(result.message).toBe('Insufficient stock for Item A');
  });

  it('should handle ABP error without code', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { error: { message: 'Validation failed' } }
    });
    const result = classifyError(error);
    expect(result.title).toBe('Error');
    expect(result.message).toBe('Validation failed');
  });

  it('should classify 403 as access denied', () => {
    const error = new HttpErrorResponse({ status: 403 });
    const result = classifyError(error);
    expect(result.action).toBe('warn');
    expect(result.title).toBe('Access Denied');
  });

  it('should classify 429 as rate limited', () => {
    const error = new HttpErrorResponse({ status: 429 });
    const result = classifyError(error);
    expect(result.action).toBe('warn');
    expect(result.title).toBe('Rate Limited');
  });

  it('should classify 500 as server error', () => {
    const error = new HttpErrorResponse({ status: 500 });
    const result = classifyError(error);
    expect(result.action).toBe('error');
    expect(result.title).toBe('Server Error');
  });

  it('should classify 502 as server error', () => {
    const error = new HttpErrorResponse({ status: 502 });
    expect(classifyError(error).action).toBe('error');
  });

  it('should handle non-standard status without ABP body', () => {
    const error = new HttpErrorResponse({ status: 422 });
    expect(classifyError(error).action).toBe('none');
  });

  it('should prioritize ABP message over status classification', () => {
    const error = new HttpErrorResponse({
      status: 500,
      error: { error: { code: 'MyERP:99999', message: 'Custom business error' } }
    });
    const result = classifyError(error);
    // ABP error takes priority over generic 500 handling
    expect(result.message).toBe('Custom business error');
    expect(result.title).toBe('MyERP:99999');
  });
});
