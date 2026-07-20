import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

/**
 * Tests for Payment Entry form multi-invoice allocation logic.
 * Critical: ensures the outstanding invoices → references mapping works correctly.
 */
describe('PaymentEntry form allocation logic', () => {
  const fb = new FormBuilder();

  interface OutstandingInvoice {
    id: string;
    invoiceNumber: string;
    outstanding: number;
    selected: boolean;
    allocatedAmount: number;
  }

  function calculateUnallocated(paymentAmount: number, invoices: OutstandingInvoice[]): number {
    const totalAllocated = invoices
      .filter(i => i.selected)
      .reduce((sum, i) => sum + i.allocatedAmount, 0);
    return paymentAmount - totalAllocated;
  }

  function buildReferences(invoices: OutstandingInvoice[]): any[] {
    return invoices
      .filter(i => i.selected && i.allocatedAmount > 0)
      .map(i => ({
        referenceType: 'SalesInvoice',
        referenceId: i.id,
        allocatedAmount: i.allocatedAmount,
      }));
  }

  describe('unallocated amount calculation', () => {
    it('should be full amount when nothing allocated', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: false, allocatedAmount: 0 },
      ];
      expect(calculateUnallocated(10000, invoices)).toBe(10000);
    });

    it('should reduce by selected allocations', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: true, allocatedAmount: 3000 },
        { id: '2', invoiceNumber: 'SI-002', outstanding: 8000, selected: true, allocatedAmount: 4000 },
      ];
      expect(calculateUnallocated(10000, invoices)).toBe(3000);
    });

    it('should be zero when fully allocated', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: true, allocatedAmount: 5000 },
        { id: '2', invoiceNumber: 'SI-002', outstanding: 8000, selected: true, allocatedAmount: 5000 },
      ];
      expect(calculateUnallocated(10000, invoices)).toBe(0);
    });

    it('should ignore unselected invoices', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: false, allocatedAmount: 5000 },
      ];
      expect(calculateUnallocated(10000, invoices)).toBe(10000);
    });
  });

  describe('references building', () => {
    it('should only include selected invoices with amount > 0', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: true, allocatedAmount: 3000 },
        { id: '2', invoiceNumber: 'SI-002', outstanding: 8000, selected: false, allocatedAmount: 0 },
        { id: '3', invoiceNumber: 'SI-003', outstanding: 2000, selected: true, allocatedAmount: 2000 },
      ];
      const refs = buildReferences(invoices);
      expect(refs.length).toBe(2);
      expect(refs[0].referenceId).toBe('1');
      expect(refs[0].allocatedAmount).toBe(3000);
      expect(refs[1].referenceId).toBe('3');
      expect(refs[1].allocatedAmount).toBe(2000);
    });

    it('should exclude selected invoices with zero amount', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: true, allocatedAmount: 0 },
      ];
      const refs = buildReferences(invoices);
      expect(refs.length).toBe(0);
    });

    it('should return empty for no selections', () => {
      const invoices: OutstandingInvoice[] = [];
      expect(buildReferences(invoices)).toEqual([]);
    });
  });

  describe('smart auto-allocation', () => {
    function autoAllocate(paymentAmount: number, invoices: OutstandingInvoice[], selectedId: string): OutstandingInvoice[] {
      let remaining = paymentAmount - invoices
        .filter(i => i.selected && i.id !== selectedId)
        .reduce((sum, i) => sum + i.allocatedAmount, 0);

      return invoices.map(inv => {
        if (inv.id === selectedId) {
          const allocated = Math.min(remaining, inv.outstanding);
          return { ...inv, selected: true, allocatedAmount: allocated };
        }
        return inv;
      });
    }

    it('should allocate min(remaining, outstanding) on select', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: false, allocatedAmount: 0 },
      ];
      const result = autoAllocate(3000, invoices, '1');
      expect(result[0].allocatedAmount).toBe(3000); // min(3000, 5000)
    });

    it('should cap at outstanding amount', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 2000, selected: false, allocatedAmount: 0 },
      ];
      const result = autoAllocate(10000, invoices, '1');
      expect(result[0].allocatedAmount).toBe(2000); // min(10000, 2000)
    });

    it('should consider already-allocated amounts from other invoices', () => {
      const invoices: OutstandingInvoice[] = [
        { id: '1', invoiceNumber: 'SI-001', outstanding: 5000, selected: true, allocatedAmount: 4000 },
        { id: '2', invoiceNumber: 'SI-002', outstanding: 8000, selected: false, allocatedAmount: 0 },
      ];
      // Payment=10000, already used=4000, remaining=6000
      const result = autoAllocate(10000, invoices, '2');
      expect(result[1].allocatedAmount).toBe(6000); // min(6000, 8000)
    });
  });
});

describe('PaymentEntry form validation', () => {
  const fb = new FormBuilder();

  function createPEForm() {
    return fb.group({
      companyId: ['comp-1', Validators.required],
      paymentType: ['Receive'],
      partyType: ['Customer'],
      partyId: ['', Validators.required],
      paidFromAccountId: ['', Validators.required],
      paidToAccountId: ['', Validators.required],
      amount: [0, [Validators.required, Validators.min(0.01)]],
      paymentDate: ['2026-07-20'],
      referenceNumber: [''],
    });
  }

  it('should be invalid without required fields', () => {
    const form = createPEForm();
    form.patchValue({ partyId: '', paidFromAccountId: '', paidToAccountId: '' });
    expect(form.valid).toBe(false);
  });

  it('should reject zero amount', () => {
    const form = createPEForm();
    form.patchValue({ amount: 0 });
    expect(form.get('amount')?.valid).toBe(false);
  });

  it('should accept valid payment', () => {
    const form = createPEForm();
    form.patchValue({
      partyId: 'cust-1',
      paidFromAccountId: 'acc-1',
      paidToAccountId: 'acc-2',
      amount: 5000,
    });
    expect(form.valid).toBe(true);
  });
});
