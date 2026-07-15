import { describe, it, expect } from 'vitest';
import { LhdnStatusBadgeComponent } from './lhdn-status-badge.component';

describe('LhdnStatusBadgeComponent', () => {
  function createComponent(status: string): LhdnStatusBadgeComponent {
    const comp = new LhdnStatusBadgeComponent();
    comp.status = status;
    return comp;
  }

  it('should return Valid config', () => {
    const comp = createComponent('Valid');
    expect(comp.config.badgeClass).toBe('bg-success');
    expect(comp.config.label).toBe('Valid');
    expect(comp.config.icon).toContain('check-circle');
  });

  it('should return Invalid config', () => {
    const comp = createComponent('Invalid');
    expect(comp.config.badgeClass).toBe('bg-danger');
    expect(comp.config.label).toBe('Invalid');
  });

  it('should return Submitted config', () => {
    const comp = createComponent('Submitted');
    expect(comp.config.badgeClass).toBe('bg-info');
  });

  it('should return Cancelled config', () => {
    const comp = createComponent('Cancelled');
    expect(comp.config.badgeClass).toBe('bg-secondary');
  });

  it('should return Failed config', () => {
    const comp = createComponent('Failed');
    expect(comp.config.badgeClass).toContain('bg-warning');
  });

  it('should return NotSubmitted config', () => {
    const comp = createComponent('NotSubmitted');
    expect(comp.config.label).toBe('Not Submitted');
    expect(comp.config.badgeClass).toBe('bg-secondary');
  });

  it('should fallback to NotSubmitted for unknown status', () => {
    const comp = createComponent('SomeRandomStatus');
    expect(comp.config.label).toBe('Not Submitted');
    expect(comp.config.badgeClass).toBe('bg-secondary');
  });

  it('should handle all LHDN statuses', () => {
    const statuses = ['Valid', 'Invalid', 'Submitted', 'Cancelled', 'Failed', 'NotSubmitted'];
    for (const status of statuses) {
      const comp = createComponent(status);
      expect(comp.config).toBeDefined();
      expect(comp.config.icon).toBeTruthy();
      expect(comp.config.badgeClass).toBeTruthy();
      expect(comp.config.label).toBeTruthy();
    }
  });
});
