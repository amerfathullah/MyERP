import { describe, it, expect } from 'vitest';
import { StatusBadgeComponent } from './status-badge.component';

describe('StatusBadgeComponent', () => {
  function createComponent(status: string): StatusBadgeComponent {
    const comp = new StatusBadgeComponent();
    comp.status = status;
    return comp;
  }

  it('should return Draft config for unknown status', () => {
    const comp = createComponent('UnknownStatus');
    expect(comp.config.badgeClass).toBe('bg-secondary');
    expect(comp.config.icon).toBe('fa fa-file');
  });

  it('should return correct config for Draft', () => {
    const comp = createComponent('Draft');
    expect(comp.config.badgeClass).toBe('bg-secondary');
  });

  it('should return correct config for Posted', () => {
    const comp = createComponent('Posted');
    expect(comp.config.badgeClass).toBe('bg-success');
    expect(comp.config.icon).toBe('fa fa-check-double');
  });

  it('should return correct config for Cancelled', () => {
    const comp = createComponent('Cancelled');
    expect(comp.config.badgeClass).toBe('bg-danger');
    expect(comp.config.icon).toBe('fa fa-ban');
  });

  it('should return correct config for ToDeliverAndBill', () => {
    const comp = createComponent('ToDeliverAndBill');
    expect(comp.config.badgeClass).toBe('bg-info');
    expect(comp.config.icon).toBe('fa fa-truck');
  });

  it('should return correct config for Completed', () => {
    const comp = createComponent('Completed');
    expect(comp.config.badgeClass).toBe('bg-success');
  });

  it('should return correct config for Closed', () => {
    const comp = createComponent('Closed');
    expect(comp.config.badgeClass).toBe('bg-dark');
    expect(comp.config.icon).toBe('fa fa-lock');
  });

  it('should return correct config for Submitted', () => {
    const comp = createComponent('Submitted');
    expect(comp.config.badgeClass).toBe('bg-info');
  });

  it('should return correct config for Overdue', () => {
    const comp = createComponent('Overdue');
    expect(comp.config.badgeClass).toBe('bg-danger');
  });

  it('should handle all document statuses without error', () => {
    const statuses = [
      'Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled', 'Rejected',
      'Overdue', 'Paid', 'PartiallyPaid', 'Active', 'Inactive',
      'ToDeliverAndBill', 'ToDeliver', 'ToBill', 'Completed', 'Closed'
    ];
    for (const status of statuses) {
      const comp = createComponent(status);
      expect(comp.config).toBeDefined();
      expect(comp.config.badgeClass).toBeTruthy();
      expect(comp.config.icon).toBeTruthy();
    }
  });
});
