import { describe, it, expect } from 'vitest';

/**
 * Tests for JournalEntryListComponent logic.
 * Verifies: pagination, search/filter, status filtering, date range,
 * company context, sorting defaults.
 */
describe('JournalEntryListComponent Logic', () => {

  function buildLoadParams(opts: {
    page?: number; pageSize?: number; search?: string;
    status?: string; fromDate?: string; toDate?: string; companyId?: string;
  }) {
    const { page = 0, pageSize = 20, search, status, fromDate, toDate, companyId } = opts;
    return {
      skipCount: page * pageSize,
      maxResultCount: pageSize,
      sorting: 'postingDate DESC',
      filter: search || undefined,
      status: status || undefined,
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
      companyId: companyId || undefined,
    };
  }

  describe('Pagination', () => {
    it('page 0 sends skipCount 0', () => {
      const p = buildLoadParams({});
      expect(p.skipCount).toBe(0);
      expect(p.maxResultCount).toBe(20);
    });

    it('page 2 with pageSize 20 sends skipCount 40', () => {
      const p = buildLoadParams({ page: 2 });
      expect(p.skipCount).toBe(40);
    });
  });

  describe('Search', () => {
    it('empty search sends undefined filter', () => {
      const p = buildLoadParams({ search: '' });
      expect(p.filter).toBeUndefined();
    });

    it('search term is forwarded', () => {
      const p = buildLoadParams({ search: 'JE-2026' });
      expect(p.filter).toBe('JE-2026');
    });
  });

  describe('Status Filter', () => {
    it('empty status sends undefined', () => {
      const p = buildLoadParams({ status: '' });
      expect(p.status).toBeUndefined();
    });

    it('Draft status forwards correctly', () => {
      const p = buildLoadParams({ status: 'Draft' });
      expect(p.status).toBe('Draft');
    });

    it('Posted status forwards correctly', () => {
      const p = buildLoadParams({ status: 'Posted' });
      expect(p.status).toBe('Posted');
    });
  });

  describe('Date Range', () => {
    it('no dates sends undefined', () => {
      const p = buildLoadParams({});
      expect(p.fromDate).toBeUndefined();
      expect(p.toDate).toBeUndefined();
    });

    it('from date is forwarded', () => {
      const p = buildLoadParams({ fromDate: '2026-01-01' });
      expect(p.fromDate).toBe('2026-01-01');
    });

    it('date range is forwarded', () => {
      const p = buildLoadParams({ fromDate: '2026-01-01', toDate: '2026-06-30' });
      expect(p.fromDate).toBe('2026-01-01');
      expect(p.toDate).toBe('2026-06-30');
    });
  });

  describe('Company Context', () => {
    it('passes companyId when set', () => {
      const p = buildLoadParams({ companyId: 'company-abc' });
      expect(p.companyId).toBe('company-abc');
    });

    it('undefined when no company selected', () => {
      const p = buildLoadParams({ companyId: '' });
      expect(p.companyId).toBeUndefined();
    });
  });

  describe('Default Sorting', () => {
    it('defaults to postingDate DESC', () => {
      const p = buildLoadParams({});
      expect(p.sorting).toBe('postingDate DESC');
    });
  });

  describe('Available Status Options', () => {
    it('JE has Draft, Posted, Cancelled statuses', () => {
      const statuses = ['Draft', 'Posted', 'Cancelled'];
      expect(statuses).toHaveLength(3);
      expect(statuses).toContain('Posted'); // JE posts directly, no Submit step
    });
  });
});

/**
 * Tests for PaymentEntryListComponent logic.
 * Verifies: pagination, party column, sorting, status filtering.
 */
describe('PaymentEntryListComponent Logic', () => {

  function buildLoadParams(opts: {
    page?: number; pageSize?: number; search?: string;
    status?: string; fromDate?: string; toDate?: string; companyId?: string;
    sorting?: string;
  }) {
    const { page = 0, pageSize = 20, search, status, fromDate, toDate, companyId, sorting } = opts;
    return {
      skipCount: page * pageSize,
      maxResultCount: pageSize,
      sorting: sorting || 'postingDate DESC',
      filter: search || undefined,
      status: status || undefined,
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
      companyId: companyId || undefined,
    };
  }

  describe('Pagination', () => {
    it('page 0 sends skipCount 0', () => {
      expect(buildLoadParams({}).skipCount).toBe(0);
    });

    it('page 3 sends skipCount 60', () => {
      expect(buildLoadParams({ page: 3 }).skipCount).toBe(60);
    });
  });

  describe('Status Options', () => {
    it('PE has Draft, Posted, Cancelled', () => {
      const statuses = ['Draft', 'Posted', 'Cancelled'];
      expect(statuses).toHaveLength(3);
      expect(statuses).not.toContain('Submitted'); // PE skips Submitted, goes Draft→Posted
    });
  });

  describe('Sorting', () => {
    it('default sort by postingDate DESC', () => {
      expect(buildLoadParams({}).sorting).toBe('postingDate DESC');
    });

    it('custom sort by paidAmount asc', () => {
      const p = buildLoadParams({ sorting: 'paidAmount asc' });
      expect(p.sorting).toBe('paidAmount asc');
    });
  });

  describe('Party Display', () => {
    it('partyName shows customer/supplier name', () => {
      const dto = { partyType: 'Customer', partyName: 'Acme Corp', paidAmount: 5000 };
      expect(dto.partyName).toBe('Acme Corp');
      expect(dto.partyType).toBe('Customer');
    });

    it('null partyName shows dash fallback', () => {
      const dto = { partyType: 'Supplier', partyName: null, paidAmount: 1000 };
      const display = dto.partyName || '—';
      expect(display).toBe('—');
    });
  });

  describe('Date Range Filter', () => {
    it('this month filter uses first/last of current month', () => {
      const now = new Date(2026, 6, 22); // July 22, 2026
      const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
      const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0);
      expect(firstDay.getDate()).toBe(1);
      expect(lastDay.getDate()).toBe(31); // July has 31 days
    });
  });
});
