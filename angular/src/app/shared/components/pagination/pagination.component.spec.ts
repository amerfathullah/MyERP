import { describe, it, expect } from 'vitest';
import { PaginationComponent, PageEvent } from './pagination.component';

describe('PaginationComponent', () => {
  function createComponent(overrides: Partial<PaginationComponent> = {}): PaginationComponent {
    const comp = new PaginationComponent();
    Object.assign(comp, overrides);
    return comp;
  }

  describe('totalPages', () => {
    it('should calculate total pages correctly', () => {
      const comp = createComponent({ totalCount: 100, pageSize: 20 });
      expect(comp.totalPages).toBe(5);
    });

    it('should round up for partial pages', () => {
      const comp = createComponent({ totalCount: 101, pageSize: 20 });
      expect(comp.totalPages).toBe(6);
    });

    it('should return 0 for empty list', () => {
      const comp = createComponent({ totalCount: 0, pageSize: 20 });
      expect(comp.totalPages).toBe(0);
    });

    it('should return 1 when items fit in one page', () => {
      const comp = createComponent({ totalCount: 15, pageSize: 20 });
      expect(comp.totalPages).toBe(1);
    });
  });

  describe('startItem / endItem', () => {
    it('should calculate start/end for first page', () => {
      const comp = createComponent({ totalCount: 50, pageSize: 20, currentPage: 0 });
      expect(comp.startItem).toBe(1);
      expect(comp.endItem).toBe(20);
    });

    it('should calculate start/end for middle page', () => {
      const comp = createComponent({ totalCount: 50, pageSize: 20, currentPage: 1 });
      expect(comp.startItem).toBe(21);
      expect(comp.endItem).toBe(40);
    });

    it('should cap endItem at totalCount for last page', () => {
      const comp = createComponent({ totalCount: 50, pageSize: 20, currentPage: 2 });
      expect(comp.startItem).toBe(41);
      expect(comp.endItem).toBe(50);
    });
  });

  describe('visiblePages', () => {
    it('should show max 5 pages', () => {
      const comp = createComponent({ totalCount: 200, pageSize: 20, currentPage: 5 });
      expect(comp.visiblePages.length).toBeLessThanOrEqual(5);
    });

    it('should center on current page', () => {
      const comp = createComponent({ totalCount: 200, pageSize: 20, currentPage: 5 });
      expect(comp.visiblePages).toContain(5);
    });

    it('should show all pages when fewer than 5 total', () => {
      const comp = createComponent({ totalCount: 60, pageSize: 20, currentPage: 0 });
      expect(comp.visiblePages).toEqual([0, 1, 2]);
    });
  });

  describe('goToPage', () => {
    it('should emit pageChange event', () => {
      const comp = createComponent({ totalCount: 100, pageSize: 20, currentPage: 0 });
      const events: PageEvent[] = [];
      comp.pageChange.subscribe((e: PageEvent) => events.push(e));

      comp.goToPage(2);

      expect(events.length).toBe(1);
      expect(events[0]).toEqual({ pageIndex: 2, pageSize: 20 });
    });

    it('should update currentPage', () => {
      const comp = createComponent({ totalCount: 100, pageSize: 20, currentPage: 0 });
      comp.pageChange.subscribe(() => {});
      comp.goToPage(3);
      expect(comp.currentPage).toBe(3);
    });

    it('should not navigate below page 0', () => {
      const comp = createComponent({ totalCount: 100, pageSize: 20, currentPage: 0 });
      const events: PageEvent[] = [];
      comp.pageChange.subscribe((e: PageEvent) => events.push(e));

      comp.goToPage(-1);

      expect(events.length).toBe(0);
      expect(comp.currentPage).toBe(0);
    });

    it('should not navigate beyond last page', () => {
      const comp = createComponent({ totalCount: 100, pageSize: 20, currentPage: 4 });
      const events: PageEvent[] = [];
      comp.pageChange.subscribe((e: PageEvent) => events.push(e));

      comp.goToPage(5);

      expect(events.length).toBe(0);
    });
  });
});
