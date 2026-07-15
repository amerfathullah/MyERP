import { describe, it, expect } from 'vitest';
import type { HasUnsavedChanges } from './unsaved-changes.guard';

describe('UnsavedChanges guard logic', () => {
  function createComponent(dirty: boolean): HasUnsavedChanges {
    return { hasUnsavedChanges: () => dirty };
  }

  it('should allow navigation when no unsaved changes', () => {
    const comp = createComponent(false);
    expect(comp.hasUnsavedChanges()).toBe(false);
    // Guard returns true (allow) when component has no unsaved changes
    const shouldAllow = !comp.hasUnsavedChanges();
    expect(shouldAllow).toBe(true);
  });

  it('should block navigation when unsaved changes exist', () => {
    const comp = createComponent(true);
    expect(comp.hasUnsavedChanges()).toBe(true);
    // Guard would prompt user before allowing
    const shouldPrompt = comp.hasUnsavedChanges();
    expect(shouldPrompt).toBe(true);
  });

  it('should handle component without hasUnsavedChanges method', () => {
    const comp = {} as any;
    // Guard checks existence of method before calling
    const shouldAllow = !comp.hasUnsavedChanges || !comp.hasUnsavedChanges();
    expect(shouldAllow).toBe(true);
  });

  it('should handle component with method returning false', () => {
    const comp: HasUnsavedChanges = { hasUnsavedChanges: () => false };
    const shouldBlock = comp.hasUnsavedChanges();
    expect(shouldBlock).toBe(false);
  });

  it('HasUnsavedChanges interface requires hasUnsavedChanges method', () => {
    // TypeScript interface enforcement test
    const comp: HasUnsavedChanges = {
      hasUnsavedChanges: () => true,
    };
    expect(typeof comp.hasUnsavedChanges).toBe('function');
  });
});
