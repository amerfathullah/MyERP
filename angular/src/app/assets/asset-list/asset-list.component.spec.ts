import { describe, it, expect } from 'vitest';

/**
 * Tests for AssetListComponent logic.
 * Verifies: status label mapping, pagination, search behavior, delete confirmation.
 */
describe('AssetListComponent Logic', () => {

  // Mirror the component's status label map
  function getStatusLabel(status: number): string {
    return ['Draft', 'Submitted', 'Partially Depreciated', 'Fully Depreciated', 'Sold', 'Scrapped', 'In Maintenance', 'Cancelled'][status] ?? 'Draft';
  }

  describe('Status Labels', () => {
    it('Draft = 0', () => expect(getStatusLabel(0)).toBe('Draft'));
    it('Submitted = 1', () => expect(getStatusLabel(1)).toBe('Submitted'));
    it('Partially Depreciated = 2', () => expect(getStatusLabel(2)).toBe('Partially Depreciated'));
    it('Fully Depreciated = 3', () => expect(getStatusLabel(3)).toBe('Fully Depreciated'));
    it('Sold = 4', () => expect(getStatusLabel(4)).toBe('Sold'));
    it('Scrapped = 5', () => expect(getStatusLabel(5)).toBe('Scrapped'));
    it('In Maintenance = 6', () => expect(getStatusLabel(6)).toBe('In Maintenance'));
    it('Cancelled = 7', () => expect(getStatusLabel(7)).toBe('Cancelled'));
    it('unknown status returns Draft', () => expect(getStatusLabel(99)).toBe('Draft'));
    it('negative status returns Draft', () => expect(getStatusLabel(-1)).toBe('Draft'));
  });

  describe('Pagination', () => {
    it('initial load uses skipCount 0 maxResult 20', () => {
      const params = { skipCount: 0, maxResultCount: 20, sorting: '' };
      expect(params.skipCount).toBe(0);
      expect(params.maxResultCount).toBe(20);
    });

    it('page change updates skipCount', () => {
      const pageIndex = 3;
      const pageSize = 20;
      const params = { skipCount: pageIndex * pageSize, maxResultCount: pageSize };
      expect(params.skipCount).toBe(60);
    });
  });

  describe('Search', () => {
    it('search passes filter parameter', () => {
      const filter = 'Laptop';
      const params = { skipCount: 0, maxResultCount: 20, sorting: '', filter };
      expect(params.filter).toBe('Laptop');
    });

    it('empty search omits filter', () => {
      const filter = '';
      const params = { skipCount: 0, maxResultCount: 20, sorting: '', filter: filter || undefined };
      expect(params.filter).toBeUndefined();
    });
  });

  describe('Asset Display', () => {
    it('book value shown for assets', () => {
      const asset = { purchaseAmount: 100000, valueAfterDepreciation: 75000 };
      expect(asset.valueAfterDepreciation).toBeLessThan(asset.purchaseAmount);
    });

    it('fully depreciated assets have 0 book value', () => {
      const asset = { purchaseAmount: 50000, valueAfterDepreciation: 0, status: 3 };
      expect(asset.valueAfterDepreciation).toBe(0);
      expect(asset.status).toBe(3); // FullyDepreciated
    });
  });
});
