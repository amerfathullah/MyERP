import { describe, it, expect } from 'vitest';
import { TaxCalculationService } from './tax-calculation.service';

describe('TaxCalculationService', () => {
  const service = new TaxCalculationService();

  describe('calculateItemAmount', () => {
    it('should calculate simple amount without discount', () => {
      const result = service.calculateItemAmount({ qty: 10, rate: 50 });
      expect(result.amount).toBe(500);
      expect(result.discountPercent).toBe(0);
      expect(result.discountAmount).toBe(0);
    });

    it('should apply percentage discount', () => {
      const result = service.calculateItemAmount({ qty: 5, rate: 100, discountPercent: 10 });
      expect(result.amount).toBe(450); // 500 - 10% = 450
      expect(result.discountAmount).toBe(50);
    });

    it('should handle zero quantity', () => {
      const result = service.calculateItemAmount({ qty: 0, rate: 100 });
      expect(result.amount).toBe(0);
    });

    it('should handle 100% discount', () => {
      const result = service.calculateItemAmount({ qty: 2, rate: 50, discountPercent: 100 });
      expect(result.amount).toBe(0);
      expect(result.discountAmount).toBe(100);
    });

    it('should round to 2 decimal places', () => {
      const result = service.calculateItemAmount({ qty: 3, rate: 33.33, discountPercent: 0 });
      expect(result.amount).toBe(99.99);
    });
  });

  describe('calculateNetTotal', () => {
    it('should sum all item amounts', () => {
      const items = [
        { qty: 2, rate: 100 },
        { qty: 3, rate: 50 },
      ];
      expect(service.calculateNetTotal(items)).toBe(350);
    });

    it('should return 0 for empty items', () => {
      expect(service.calculateNetTotal([])).toBe(0);
    });

    it('should account for discounts', () => {
      const items = [
        { qty: 1, rate: 100, discountPercent: 20 }, // 80
        { qty: 1, rate: 200 }, // 200
      ];
      expect(service.calculateNetTotal(items)).toBe(280);
    });
  });

  describe('calculateTaxes', () => {
    it('should calculate tax on net total', () => {
      const taxes = service.calculateTaxes(1000, [
        { taxName: 'SST', rate: 6, chargeType: 'OnNetTotal' },
      ]);
      expect(taxes.length).toBe(1);
      expect(taxes[0].taxAmount).toBe(60);
      expect(taxes[0].total).toBe(1060);
    });

    it('should cascade on previous row total', () => {
      const taxes = service.calculateTaxes(1000, [
        { taxName: 'SST', rate: 10, chargeType: 'OnNetTotal' },
        { taxName: 'Service Tax', rate: 5, chargeType: 'OnPreviousRowTotal' },
      ]);
      expect(taxes[0].taxAmount).toBe(100); // 10% of 1000
      expect(taxes[0].total).toBe(1100);
      expect(taxes[1].taxAmount).toBe(55); // 5% of 1100
      expect(taxes[1].total).toBe(1155);
    });

    it('should handle actual (fixed) amounts', () => {
      const taxes = service.calculateTaxes(500, [
        { taxName: 'Stamp Duty', rate: 0, chargeType: 'Actual', actualAmount: 10 },
      ]);
      expect(taxes[0].taxAmount).toBe(10);
      expect(taxes[0].total).toBe(510);
    });

    it('should handle zero tax rate', () => {
      const taxes = service.calculateTaxes(1000, [
        { taxName: 'Exempt', rate: 0, chargeType: 'OnNetTotal' },
      ]);
      expect(taxes[0].taxAmount).toBe(0);
      expect(taxes[0].total).toBe(1000);
    });

    it('should return empty array for no tax rules', () => {
      const taxes = service.calculateTaxes(1000, []);
      expect(taxes.length).toBe(0);
    });
  });

  describe('calculate (full pipeline)', () => {
    it('should calculate complete invoice with SST 6%', () => {
      const result = service.calculate(
        [{ qty: 10, rate: 100 }],
        [{ taxName: 'SST', rate: 6, chargeType: 'OnNetTotal' }]
      );
      expect(result.netTotal).toBe(1000);
      expect(result.totalTax).toBe(60);
      expect(result.grandTotal).toBe(1060);
    });

    it('should handle multi-item with discount + tax', () => {
      const result = service.calculate(
        [
          { qty: 5, rate: 200, discountPercent: 10 }, // 900
          { qty: 2, rate: 150 }, // 300
        ],
        [{ taxName: 'SST', rate: 6, chargeType: 'OnNetTotal' }]
      );
      expect(result.netTotal).toBe(1200);
      expect(result.totalTax).toBe(72);
      expect(result.grandTotal).toBe(1272);
    });

    it('should handle zero items', () => {
      const result = service.calculate([], [{ taxName: 'SST', rate: 6, chargeType: 'OnNetTotal' }]);
      expect(result.netTotal).toBe(0);
      expect(result.totalTax).toBe(0);
      expect(result.grandTotal).toBe(0);
    });

    it('should handle multiple tax lines (cascading)', () => {
      const result = service.calculate(
        [{ qty: 1, rate: 1000 }],
        [
          { taxName: 'SST', rate: 10, chargeType: 'OnNetTotal' },
          { taxName: 'Cess', rate: 1, chargeType: 'OnPreviousRowTotal' },
        ]
      );
      expect(result.netTotal).toBe(1000);
      expect(result.taxLines[0].taxAmount).toBe(100);
      expect(result.taxLines[1].taxAmount).toBe(11); // 1% of 1100
      expect(result.totalTax).toBe(111);
      expect(result.grandTotal).toBe(1111);
    });
  });
});
