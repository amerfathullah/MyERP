import { describe, it, expect, beforeEach } from 'vitest';

interface OutstandingInvoice {
  voucherId: string;
  voucherType: string;
  documentNumber?: string;
  postingDate?: string;
  grandTotal?: number;
  outstanding: number;
  allocatedAmount: number;
  selected: boolean;
}

interface UnreconciledPayment {
  voucherId: string;
  voucherType: string;
  documentNumber?: string;
  postingDate?: string;
  totalAmount: number;
  unallocatedAmount: number;
  currencyCode?: string;
  exchangeRate?: number;
}

class MockPaymentReconciliationLogic {
  invoices: OutstandingInvoice[] = [];
  payments: UnreconciledPayment[] = [];
  selectedPaymentVoucherId: string | null = null;

  get selectedPayment(): UnreconciledPayment | undefined {
    return this.payments.find(p => p.voucherId === this.selectedPaymentVoucherId);
  }

  selectPayment(voucherId: string) {
    this.selectedPaymentVoucherId = voucherId;
    this.invoices = this.invoices.map(i => ({ ...i, selected: false, allocatedAmount: 0 }));
  }

  get totalAllocated(): number {
    return this.invoices.filter(i => i.selected).reduce((sum, i) => sum + i.allocatedAmount, 0);
  }

  get overAllocated(): boolean {
    const payment = this.selectedPayment;
    return !!payment && this.totalAllocated > payment.unallocatedAmount + 0.009;
  }

  get totalInvoiceAmount(): number {
    const selected = this.invoices.filter(i => i.selected);
    const list = selected.length > 0 ? selected : this.invoices;
    return list.reduce((sum, i) => sum + (i.outstanding || 0), 0);
  }

  get totalPaymentAmount(): number {
    const selected = this.selectedPayment;
    if (selected) return selected.unallocatedAmount || 0;
    return this.payments.reduce((sum, p) => sum + (p.unallocatedAmount || 0), 0);
  }

  get differenceAmount(): number {
    return Math.round((this.totalInvoiceAmount - this.totalPaymentAmount) * 100) / 100;
  }
}

describe('PaymentReconciliationComponent Logic', () => {
  let comp: MockPaymentReconciliationLogic;

  beforeEach(() => {
    comp = new MockPaymentReconciliationLogic();
    comp.invoices = [
      {
        voucherId: 'inv-1',
        voucherType: 'SalesInvoice',
        documentNumber: 'SI-001',
        outstanding: 1000,
        allocatedAmount: 0,
        selected: false,
      },
      {
        voucherId: 'inv-2',
        voucherType: 'SalesInvoice',
        documentNumber: 'SI-002',
        outstanding: 500,
        allocatedAmount: 0,
        selected: false,
      },
    ];

    comp.payments = [
      {
        voucherId: 'pay-1',
        voucherType: 'PaymentEntry',
        documentNumber: 'PE-001',
        totalAmount: 1200,
        unallocatedAmount: 1200,
        currencyCode: 'MYR',
      },
      {
        voucherId: 'pay-2',
        voucherType: 'PaymentEntry',
        documentNumber: 'PE-002',
        totalAmount: 400,
        unallocatedAmount: 400,
        currencyCode: 'MYR',
      },
    ];
  });

  describe('Totals Section (ERPNext PR #59233 / commit 5aeacbbf5d)', () => {
    it('calculates total invoice amount from all invoices when none selected', () => {
      expect(comp.totalInvoiceAmount).toBe(1500);
    });

    it('calculates total invoice amount from selected invoices only when some selected', () => {
      comp.invoices[0].selected = true;
      expect(comp.totalInvoiceAmount).toBe(1000);

      comp.invoices[1].selected = true;
      expect(comp.totalInvoiceAmount).toBe(1500);
    });

    it('calculates total payment amount from all payments when none selected', () => {
      expect(comp.totalPaymentAmount).toBe(1600); // 1200 + 400
    });

    it('calculates total payment amount from selected payment', () => {
      comp.selectPayment('pay-1');
      expect(comp.totalPaymentAmount).toBe(1200);

      comp.selectPayment('pay-2');
      expect(comp.totalPaymentAmount).toBe(400);
    });

    it('calculates difference amount (Invoice Total - Payment Total)', () => {
      // 1500 invoice vs 1600 payment => -100 difference
      expect(comp.differenceAmount).toBe(-100);

      // Select pay-1 (1200), inv-1 (1000) => difference = -200
      comp.selectPayment('pay-1');
      comp.invoices[0].selected = true;
      expect(comp.totalInvoiceAmount).toBe(1000);
      expect(comp.totalPaymentAmount).toBe(1200);
      expect(comp.differenceAmount).toBe(-200);

      // Select pay-2 (400), inv-1 (1000) => difference = +600 (underpaid)
      comp.selectPayment('pay-2');
      comp.invoices[0].selected = true;
      expect(comp.totalInvoiceAmount).toBe(1000);
      expect(comp.totalPaymentAmount).toBe(400);
      expect(comp.differenceAmount).toBe(600);
    });
  });

  describe('Allocation and Selection Guards', () => {
    it('selectPayment clears prior invoice selections and allocated amounts', () => {
      comp.invoices[0].selected = true;
      comp.invoices[0].allocatedAmount = 500;

      comp.selectPayment('pay-1');

      expect(comp.invoices[0].selected).toBe(false);
      expect(comp.invoices[0].allocatedAmount).toBe(0);
      expect(comp.totalAllocated).toBe(0);
    });

    it('detects over-allocation when totalAllocated exceeds unallocatedAmount', () => {
      comp.selectPayment('pay-2'); // 400 available
      comp.invoices[0].selected = true;
      comp.invoices[0].allocatedAmount = 450;

      expect(comp.totalAllocated).toBe(450);
      expect(comp.overAllocated).toBe(true);

      comp.invoices[0].allocatedAmount = 400;
      expect(comp.overAllocated).toBe(false);
    });
  });
});
