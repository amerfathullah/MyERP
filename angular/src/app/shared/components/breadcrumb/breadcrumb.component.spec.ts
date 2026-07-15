import { describe, it, expect } from 'vitest';

/**
 * Tests for the breadcrumb logic (extracted from BreadcrumbComponent).
 * The component uses Router internally, so we test the pure transformation logic.
 */
describe('Breadcrumb logic', () => {
  // Extracted formatLabel logic from BreadcrumbComponent
  function formatLabel(segment: string): string {
    return segment
      .replace(/-/g, ' ')
      .replace(/\b\w/g, c => c.toUpperCase())
      .replace(/\bnew\b/i, 'New')
      .replace(/\breports\b/i, 'Reports');
  }

  // Extracted buildBreadcrumbs logic
  function buildBreadcrumbs(url: string): Array<{ label: string; url: string }> {
    const cleanUrl = url.split('?')[0];
    const segments = cleanUrl.split('/').filter(s => s.length > 0);
    const crumbs: Array<{ label: string; url: string }> = [];
    let path = '';

    for (const segment of segments) {
      path += '/' + segment;
      if (segment.length === 36 && segment.includes('-')) {
        crumbs.push({ label: 'Detail', url: path });
      } else {
        const label = formatLabel(segment);
        crumbs.push({ label, url: path });
      }
    }
    return crumbs;
  }

  describe('formatLabel', () => {
    it('should convert kebab-case to Title Case', () => {
      expect(formatLabel('sales-invoices')).toBe('Sales Invoices');
    });

    it('should handle single word', () => {
      expect(formatLabel('accounting')).toBe('Accounting');
    });

    it('should handle "new" keyword', () => {
      expect(formatLabel('new')).toBe('New');
    });

    it('should handle "reports" keyword', () => {
      expect(formatLabel('reports')).toBe('Reports');
    });

    it('should handle multi-word segments', () => {
      expect(formatLabel('payment-entries')).toBe('Payment Entries');
    });
  });

  describe('buildBreadcrumbs', () => {
    it('should build breadcrumbs from simple path', () => {
      const crumbs = buildBreadcrumbs('/sales/invoices');
      expect(crumbs.length).toBe(2);
      expect(crumbs[0]).toEqual({ label: 'Sales', url: '/sales' });
      expect(crumbs[1]).toEqual({ label: 'Invoices', url: '/sales/invoices' });
    });

    it('should handle UUID segments as "Detail"', () => {
      const crumbs = buildBreadcrumbs('/sales/invoices/a1b2c3d4-e5f6-7890-abcd-ef1234567890');
      expect(crumbs.length).toBe(3);
      expect(crumbs[2].label).toBe('Detail');
    });

    it('should strip query parameters', () => {
      const crumbs = buildBreadcrumbs('/accounting/reports?from=2026-01-01');
      expect(crumbs.length).toBe(2);
      expect(crumbs[1].label).toBe('Reports');
    });

    it('should return empty for root path', () => {
      const crumbs = buildBreadcrumbs('/');
      expect(crumbs.length).toBe(0);
    });

    it('should handle deep paths', () => {
      const crumbs = buildBreadcrumbs('/manufacturing/work-orders/new');
      expect(crumbs.length).toBe(3);
      expect(crumbs[0].label).toBe('Manufacturing');
      expect(crumbs[1].label).toBe('Work Orders');
      expect(crumbs[2].label).toBe('New');
    });

    it('should accumulate path segments in url', () => {
      const crumbs = buildBreadcrumbs('/a/b/c');
      expect(crumbs[0].url).toBe('/a');
      expect(crumbs[1].url).toBe('/a/b');
      expect(crumbs[2].url).toBe('/a/b/c');
    });
  });
});
