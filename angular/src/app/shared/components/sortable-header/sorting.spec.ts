import { describe, it, expect } from 'vitest';

/**
 * Tests for the Sorting Helper utility used by 7 backend AppServices.
 * This replicates the SortingHelper logic in TypeScript to verify frontend sorting params
 * match what the backend expects.
 */
describe('Sorting parameter generation', () => {
  // Replicates the sorting params that Angular list pages send to the backend
  function buildSortParam(field: string, direction: 'asc' | 'desc'): string {
    return `${field} ${direction}`;
  }

  it('should format ascending sort', () => {
    expect(buildSortParam('invoiceNumber', 'asc')).toBe('invoiceNumber asc');
  });

  it('should format descending sort', () => {
    expect(buildSortParam('issueDate', 'desc')).toBe('issueDate desc');
  });
});

describe('SortableHeader state management', () => {
  interface SortState { field: string; direction: 'asc' | 'desc' | null; }

  function toggleSort(current: SortState, clickedField: string): SortState {
    if (current.field === clickedField) {
      if (current.direction === 'asc') return { field: clickedField, direction: 'desc' };
      if (current.direction === 'desc') return { field: '', direction: null };
      return { field: clickedField, direction: 'asc' };
    }
    return { field: clickedField, direction: 'asc' };
  }

  it('should start with ascending on first click', () => {
    const state = toggleSort({ field: '', direction: null }, 'date');
    expect(state).toEqual({ field: 'date', direction: 'asc' });
  });

  it('should toggle to descending on second click', () => {
    const state = toggleSort({ field: 'date', direction: 'asc' }, 'date');
    expect(state).toEqual({ field: 'date', direction: 'desc' });
  });

  it('should clear on third click', () => {
    const state = toggleSort({ field: 'date', direction: 'desc' }, 'date');
    expect(state).toEqual({ field: '', direction: null });
  });

  it('should reset to asc when clicking a different column', () => {
    const state = toggleSort({ field: 'date', direction: 'desc' }, 'amount');
    expect(state).toEqual({ field: 'amount', direction: 'asc' });
  });
});

describe('Date range preset calculations', () => {
  function formatLocalDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  function getDateRange(preset: string, referenceDate: Date = new Date()): { from: string; to: string } {
    const year = referenceDate.getFullYear();
    const month = referenceDate.getMonth();

    switch (preset) {
      case 'thisMonth':
        return {
          from: formatLocalDate(new Date(year, month, 1)),
          to: formatLocalDate(new Date(year, month + 1, 0)),
        };
      case 'lastMonth':
        return {
          from: formatLocalDate(new Date(year, month - 1, 1)),
          to: formatLocalDate(new Date(year, month, 0)),
        };
      case 'thisQuarter': {
        const qStart = Math.floor(month / 3) * 3;
        return {
          from: formatLocalDate(new Date(year, qStart, 1)),
          to: formatLocalDate(new Date(year, qStart + 3, 0)),
        };
      }
      default:
        return { from: '', to: '' };
    }
  }

  it('thisMonth should start on 1st and end on last day', () => {
    const ref = new Date(2026, 6, 15); // July 15, 2026
    const range = getDateRange('thisMonth', ref);
    expect(range.from).toBe('2026-07-01');
    expect(range.to).toBe('2026-07-31');
  });

  it('lastMonth should cover previous month', () => {
    const ref = new Date(2026, 6, 15); // July 15
    const range = getDateRange('lastMonth', ref);
    expect(range.from).toBe('2026-06-01');
    expect(range.to).toBe('2026-06-30');
  });

  it('thisQuarter for July should be Q3 (Jul-Sep)', () => {
    const ref = new Date(2026, 6, 15); // July = Q3
    const range = getDateRange('thisQuarter', ref);
    expect(range.from).toBe('2026-07-01');
    expect(range.to).toBe('2026-09-30');
  });

  it('thisQuarter for January should be Q1 (Jan-Mar)', () => {
    const ref = new Date(2026, 0, 10); // January = Q1
    const range = getDateRange('thisQuarter', ref);
    expect(range.from).toBe('2026-01-01');
    expect(range.to).toBe('2026-03-31');
  });

  it('unknown preset returns empty strings', () => {
    const range = getDateRange('invalid');
    expect(range.from).toBe('');
    expect(range.to).toBe('');
  });
});

describe('Bulk select logic', () => {
  interface SelectableItem { id: string; selected: boolean; status: number; }

  function getSelectableItems(items: SelectableItem[]): SelectableItem[] {
    // Only Draft (0) items can be bulk-submitted
    return items.filter(i => i.status === 0);
  }

  function getSelectedIds(items: SelectableItem[]): string[] {
    return items.filter(i => i.selected).map(i => i.id);
  }

  it('should filter only Draft items as selectable', () => {
    const items: SelectableItem[] = [
      { id: '1', selected: false, status: 0 },
      { id: '2', selected: false, status: 1 },
      { id: '3', selected: false, status: 0 },
    ];
    const selectable = getSelectableItems(items);
    expect(selectable.length).toBe(2);
    expect(selectable.map(i => i.id)).toEqual(['1', '3']);
  });

  it('should return selected IDs', () => {
    const items: SelectableItem[] = [
      { id: '1', selected: true, status: 0 },
      { id: '2', selected: false, status: 0 },
      { id: '3', selected: true, status: 0 },
    ];
    expect(getSelectedIds(items)).toEqual(['1', '3']);
  });

  it('should return empty when none selected', () => {
    const items: SelectableItem[] = [
      { id: '1', selected: false, status: 0 },
    ];
    expect(getSelectedIds(items)).toEqual([]);
  });
});
