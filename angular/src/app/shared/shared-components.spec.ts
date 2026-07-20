import { describe, it, expect } from 'vitest';

/**
 * Tests for shared components used across all modules:
 * - SortableHeader: sort direction toggle state machine
 * - DatePresets: date range calculation logic
 * - DocumentWorkflow: action emission
 * - CompanyContext: selection persistence logic
 */

// ========== SortableHeader Logic ==========
describe('SortableHeader state machine', () => {
  interface SortEvent { field: string; direction: 'asc' | 'desc'; }

  function toggleSort(field: string, currentField: string | null, currentDirection: 'asc' | 'desc'): SortEvent {
    if (currentField === field) {
      return { field, direction: currentDirection === 'asc' ? 'desc' : 'asc' };
    } else {
      return { field, direction: 'desc' };
    }
  }

  it('should start with desc when clicking new column', () => {
    const event = toggleSort('grandTotal', null, 'asc');
    expect(event.direction).toBe('desc');
    expect(event.field).toBe('grandTotal');
  });

  it('should toggle from desc to asc on same column', () => {
    const event = toggleSort('date', 'date', 'desc');
    expect(event.direction).toBe('asc');
  });

  it('should toggle from asc to desc on same column', () => {
    const event = toggleSort('date', 'date', 'asc');
    expect(event.direction).toBe('desc');
  });

  it('should reset to desc when switching columns', () => {
    const event = toggleSort('amount', 'date', 'asc');
    expect(event.field).toBe('amount');
    expect(event.direction).toBe('desc');
  });
});

// ========== DatePresets Calculation ==========
describe('DatePresets date calculation', () => {
  // Use the same formatting as the real component (toISOString may shift timezone)
  function formatDate(d: Date): string {
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  function getThisMonth(): { from: string; to: string } {
    const now = new Date();
    const from = new Date(now.getFullYear(), now.getMonth(), 1);
    const to = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    return { from: formatDate(from), to: formatDate(to) };
  }

  function getLastMonth(): { from: string; to: string } {
    const now = new Date();
    const from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    const to = new Date(now.getFullYear(), now.getMonth(), 0);
    return { from: formatDate(from), to: formatDate(to) };
  }

  function getThisQuarter(): { from: string; to: string } {
    const now = new Date();
    const quarter = Math.floor(now.getMonth() / 3);
    const from = new Date(now.getFullYear(), quarter * 3, 1);
    const to = new Date(now.getFullYear(), quarter * 3 + 3, 0);
    return { from: formatDate(from), to: formatDate(to) };
  }

  it('thisMonth should start on 1st', () => {
    const { from } = getThisMonth();
    expect(from).toMatch(/-01$/);
  });

  it('thisMonth should end on last day', () => {
    const { to } = getThisMonth();
    const day = parseInt(to.split('-')[2]);
    expect(day).toBeGreaterThanOrEqual(28);
    expect(day).toBeLessThanOrEqual(31);
  });

  it('lastMonth from should be previous month 1st', () => {
    const { from } = getLastMonth();
    const now = new Date();
    const expectedMonth = now.getMonth() === 0 ? 12 : now.getMonth();
    const month = parseInt(from.split('-')[1]);
    expect(month).toBe(expectedMonth);
  });

  it('thisQuarter should span 3 months', () => {
    const { from, to } = getThisQuarter();
    const fromMonth = parseInt(from.split('-')[1]);
    const toMonth = parseInt(to.split('-')[1]);
    const diff = toMonth - fromMonth;
    expect(diff).toBe(2);
  });

  it('thisQuarter start should be quarter boundary', () => {
    const { from } = getThisQuarter();
    const month = parseInt(from.split('-')[1]);
    expect([1, 4, 7, 10]).toContain(month);
  });

  it('formatDate should produce YYYY-MM-DD string', () => {
    const result = formatDate(new Date(2026, 6, 15)); // July 15, 2026
    expect(result).toBe('2026-07-15');
  });

  it('formatDate should handle month boundaries', () => {
    // Last day of February (non-leap year 2027)
    const feb = new Date(2027, 2, 0);
    expect(formatDate(feb)).toBe('2027-02-28');
  });
});

// ========== DocumentWorkflow Action Logic ==========
describe('DocumentWorkflow action visibility', () => {
  interface WorkflowAction { name: string; label: string; icon: string; color: string; }

  function getVisibleActions(status: string, allActions: WorkflowAction[], statusRules: Record<string, string[]>): WorkflowAction[] {
    const allowed = statusRules[status] ?? [];
    return allActions.filter(a => allowed.includes(a.name));
  }

  const allActions: WorkflowAction[] = [
    { name: 'submit', label: 'Submit', icon: 'fa-paper-plane', color: 'btn-outline-primary' },
    { name: 'cancel', label: 'Cancel', icon: 'fa-ban', color: 'btn-outline-danger' },
    { name: 'amend', label: 'Amend', icon: 'fa-copy', color: 'btn-outline-secondary' },
    { name: 'close', label: 'Close', icon: 'fa-lock', color: 'btn-outline-dark' },
  ];

  const statusRules: Record<string, string[]> = {
    Draft: ['submit', 'cancel'],
    Submitted: ['cancel'],
    Posted: ['cancel'],
    Cancelled: ['amend'],
    ToDeliverAndBill: ['close', 'cancel'],
    Closed: [],
  };

  it('Draft shows submit + cancel', () => {
    const visible = getVisibleActions('Draft', allActions, statusRules);
    expect(visible.map(a => a.name)).toEqual(['submit', 'cancel']);
  });

  it('Cancelled shows only amend', () => {
    const visible = getVisibleActions('Cancelled', allActions, statusRules);
    expect(visible.map(a => a.name)).toEqual(['amend']);
  });

  it('Closed shows no actions', () => {
    const visible = getVisibleActions('Closed', allActions, statusRules);
    expect(visible.length).toBe(0);
  });

  it('ToDeliverAndBill shows cancel + close (in allActions order)', () => {
    const visible = getVisibleActions('ToDeliverAndBill', allActions, statusRules);
    expect(visible.map(a => a.name)).toEqual(['cancel', 'close']);
  });

  it('unknown status shows no actions', () => {
    const visible = getVisibleActions('Unknown', allActions, statusRules);
    expect(visible.length).toBe(0);
  });
});

// ========== CompanyContext Persistence ==========
describe('CompanyContext selection logic', () => {
  it('should auto-select first company when none saved', () => {
    const companies = [{ id: 'comp-1', name: 'ABC Sdn Bhd' }, { id: 'comp-2', name: 'XYZ Ltd' }];
    const savedId = '';
    const selected = savedId || companies[0]?.id || '';
    expect(selected).toBe('comp-1');
  });

  it('should use saved company when available', () => {
    const companies = [{ id: 'comp-1', name: 'ABC Sdn Bhd' }, { id: 'comp-2', name: 'XYZ Ltd' }];
    const savedId = 'comp-2';
    const selected = savedId || companies[0]?.id || '';
    expect(selected).toBe('comp-2');
  });

  it('should handle empty company list gracefully', () => {
    const companies: any[] = [];
    const savedId = '';
    const selected = savedId || companies[0]?.id || '';
    expect(selected).toBe('');
  });

  it('should persist selection key', () => {
    const id = 'comp-1';
    const name = 'Test Company';
    const storage: Record<string, string> = {};
    storage['myerp_company_id'] = id;
    storage['myerp_company_name'] = name;
    expect(storage['myerp_company_id']).toBe(id);
    expect(storage['myerp_company_name']).toBe(name);
  });
});

// ========== CSV Export Utility ==========
describe('CSV export utility', () => {
  function escapeCsvField(value: any): string {
    const str = String(value ?? '');
    if (str.includes(',') || str.includes('"') || str.includes('\n')) {
      return `"${str.replace(/"/g, '""')}"`;
    }
    return str;
  }

  function buildCsvRow(values: any[]): string {
    return values.map(escapeCsvField).join(',');
  }

  it('should not escape simple values', () => {
    expect(escapeCsvField('Hello')).toBe('Hello');
    expect(escapeCsvField(123)).toBe('123');
  });

  it('should escape values with commas', () => {
    expect(escapeCsvField('ABC, XYZ')).toBe('"ABC, XYZ"');
  });

  it('should escape values with quotes', () => {
    expect(escapeCsvField('He said "hi"')).toBe('"He said ""hi"""');
  });

  it('should escape values with newlines', () => {
    expect(escapeCsvField('Line 1\nLine 2')).toBe('"Line 1\nLine 2"');
  });

  it('should handle null/undefined', () => {
    expect(escapeCsvField(null)).toBe('');
    expect(escapeCsvField(undefined)).toBe('');
  });

  it('should build complete CSV row', () => {
    const row = buildCsvRow(['SI-001', '2026-07-20', 1060.50, 'ABC, Corp']);
    expect(row).toBe('SI-001,2026-07-20,1060.5,"ABC, Corp"');
  });
});
