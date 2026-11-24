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
    expect(comp.config.labelKey).toBe('::Valid');
    expect(comp.config.icon).toContain('check-circle');
  });

  it('should return Invalid config', () => {
    const comp = createComponent('Invalid');
    expect(comp.config.badgeClass).toBe('bg-danger');
    expect(comp.config.labelKey).toBe('::Invalid');
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
    expect(comp.config.labelKey).toBe('::NotSubmitted');
    expect(comp.config.badgeClass).toBe('bg-secondary');
  });

  it('should fallback to NotSubmitted for unknown status', () => {
    const comp = createComponent('SomeRandomStatus');
    expect(comp.config.labelKey).toBe('::NotSubmitted');
    expect(comp.config.badgeClass).toBe('bg-secondary');
  });

  it('should return Consolidated config when isConsolidated is true regardless of status', () => {
    const comp = createComponent('Valid');
    comp.isConsolidated = true;
    expect(comp.config.labelKey).toBe('::Consolidated');
    expect(comp.config.icon).toContain('layer-group');
  });

  it('should handle all LHDN statuses', () => {
    const statuses = ['Valid', 'Invalid', 'Pending', 'Submitted', 'Cancelled', 'Rejected', 'Failed', 'NotSubmitted', 'Consolidated'];
    for (const status of statuses) {
      const comp = createComponent(status);
      expect(comp.config).toBeDefined();
      expect(comp.config.icon).toBeTruthy();
      expect(comp.config.badgeClass).toBeTruthy();
      expect(comp.config.labelKey).toBeTruthy();
    }
    expect(createComponent('Pending').config.badgeClass).toBe('bg-info');
    expect(createComponent('Pending').config.labelKey).toBe('::Pending');
    expect(createComponent('Rejected').config.badgeClass).toBe('bg-danger');
    expect(createComponent('Rejected').config.labelKey).toBe('::Rejected');
  });
});
