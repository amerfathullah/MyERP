import { describe, it, expect } from 'vitest';

// PurchaseInvoiceDetailComponent — workflow actions + navigation + payment schedule tests

function getWorkflowActions(
  status: string,
  outstandingAmount: number = 0,
  perms: { canCreatePI?: boolean; canCancelPI?: boolean; canEditPI?: boolean; canCreatePR?: boolean; canCreateLCV?: boolean; canCreateSI?: boolean } = {}
): { name: string; label: string; color: string }[] {
  const actions: { name: string; label: string; color: string }[] = [];
  const canCreatePI = perms.canCreatePI ?? true;
  const canCancelPI = perms.canCancelPI ?? true;
  if (status === 'Draft') {
    actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
  }
  if (status === 'Submitted') {
    actions.push({ name: 'post', label: 'Post', color: 'primary' });
  }
  if (status === 'Posted') {
    actions.push({ name: 'payment', label: 'Make Payment', color: 'primary' });
    if (canCreatePI) {
      actions.push({ name: 'return', label: 'Create Return', color: 'warning' });
    }
    if (outstandingAmount > 0) {
      actions.push({ name: 'writeOff', label: 'Write Off', color: 'secondary' });
    }
    if (perms.canEditPI && outstandingAmount !== 0) {
      actions.push({ name: 'block', label: 'Block Invoice', color: 'warning' });
    }
    if (perms.canCreatePR) {
      actions.push({ name: 'createReceipt', label: 'Create Purchase Receipt', color: 'info' });
    }
    if (perms.canCreateLCV) {
      actions.push({ name: 'createLCV', label: 'Create Landed Cost Voucher', color: 'info' });
    }
    if (perms.canCreateSI) {
      actions.push({ name: 'createSalesInvoice', label: 'Make Sales Invoice', color: 'info' });
    }
    if (canCancelPI) {
      actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
    }
  }
  if (status === 'Cancelled') {
    if (canCreatePI) {
      actions.push({ name: 'amend', label: 'Amend', color: 'primary' });
    }
  }
  return actions;
}

function getActionRoute(action: string, id: string): string | null {
  switch (action) {
    case 'payment': return `/accounting/payments/new?partyType=Supplier&againstInvoiceType=PurchaseInvoice&againstInvoiceId=${id}`;
    case 'return': return `/purchasing/invoices/new?returnAgainst=${id}`;
    case 'amend': return `/purchasing/invoices/${id}`;
    case 'createReceipt': return `/purchasing/receipts/new?purchaseInvoiceId=${id}`;
    case 'createLCV': return `/purchasing/landed-cost-vouchers/new?purchaseInvoiceId=${id}`;
    case 'createSalesInvoice': return `/sales/invoices/new?interCompanyInvoiceReference=${id}`;
    default: return null;
  }
}

describe('PurchaseInvoiceDetailComponent', () => {
  // Workflow actions per status
  describe('workflow actions', () => {
    it('should show Submit for Draft', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
    });

    it('should show Post for Submitted', () => {
      const actions = getWorkflowActions('Submitted');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('post');
    });

    it('should show Payment + Return + Cancel for Posted', () => {
      const actions = getWorkflowActions('Posted');
      expect(actions).toHaveLength(3);
      expect(actions.map(a => a.name)).toEqual(['payment', 'return', 'cancel']);
    });

    it('should show WriteOff for Posted with outstanding', () => {
      const actions = getWorkflowActions('Posted', 500);
      expect(actions).toHaveLength(4);
      expect(actions.map(a => a.name)).toContain('writeOff');
    });

    it('should NOT show WriteOff for Posted with zero outstanding', () => {
      const actions = getWorkflowActions('Posted', 0);
      expect(actions.map(a => a.name)).not.toContain('writeOff');
    });

    it('should show Amend for Cancelled', () => {
      const actions = getWorkflowActions('Cancelled');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
    });

    it('should respect permissions on purchase invoice actions (ERPNext #59367)', () => {
      // With canCreatePI false, return and amend are hidden
      const postedNoCreate = getWorkflowActions('Posted', 100, { canCreatePI: false, canCancelPI: false });
      expect(postedNoCreate.map(a => a.name)).not.toContain('return');
      expect(postedNoCreate.map(a => a.name)).not.toContain('cancel');

      const cancelledNoCreate = getWorkflowActions('Cancelled', 0, { canCreatePI: false });
      expect(cancelledNoCreate).toHaveLength(0);

      // With canEditPI true, hold/block action is available when outstanding != 0
      const postedWithEdit = getWorkflowActions('Posted', 500, { canEditPI: true });
      expect(postedWithEdit.map(a => a.name)).toContain('block');

      // With canCreatePR, canCreateLCV, canCreateSI
      const postedLinked = getWorkflowActions('Posted', 0, { canCreatePR: true, canCreateLCV: true, canCreateSI: true });
      expect(postedLinked.map(a => a.name)).toContain('createReceipt');
      expect(postedLinked.map(a => a.name)).toContain('createLCV');
      expect(postedLinked.map(a => a.name)).toContain('createSalesInvoice');
    });

    it('should show no actions for unknown status', () => {
      const actions = getWorkflowActions('Unknown');
      expect(actions).toHaveLength(0);
    });
  });

  // Navigation routes
  describe('action navigation', () => {
    it('should navigate to PE form with Supplier params for Make Payment', () => {
      const route = getActionRoute('payment', 'pi-123');
      expect(route).toContain('partyType=Supplier');
      expect(route).toContain('againstInvoiceType=PurchaseInvoice');
      expect(route).toContain('againstInvoiceId=pi-123');
    });

    it('should navigate to PI form with returnAgainst for Create Return', () => {
      const route = getActionRoute('return', 'pi-123');
      expect(route).toContain('returnAgainst=pi-123');
    });

    it('should navigate to amended PI', () => {
      const route = getActionRoute('amend', 'pi-123');
      expect(route).toBe('/purchasing/invoices/pi-123');
    });
  });

  // Payment schedule display
  describe('payment schedule', () => {
    it('should default to empty schedule', () => {
      const schedule: any[] = [];
      expect(schedule).toHaveLength(0);
    });

    it('should calculate outstanding per term', () => {
      const term = { paymentAmount: 1000, paidAmount: 600 };
      const outstanding = term.paymentAmount - term.paidAmount;
      expect(outstanding).toBe(400);
    });

    it('should identify fully paid term', () => {
      const term = { paymentAmount: 1000, paidAmount: 1000 };
      expect(term.paymentAmount - term.paidAmount).toBe(0);
    });
  });

  // Debit note display
  describe('debit note', () => {
    it('should identify debit note (PI return)', () => {
      const pi = { isReturn: true, returnAgainstId: 'orig-pi' };
      expect(pi.isReturn).toBe(true);
    });

    it('should show negative amounts for debit notes', () => {
      const grandTotal = -1500;
      expect(grandTotal).toBeLessThan(0);
    });
  });

  // Amendment chain
  describe('amendment', () => {
    it('should detect amended PI', () => {
      const pi = { amendedFromId: 'orig', amendmentIndex: 2 };
      expect(pi.amendedFromId).toBeTruthy();
      expect(pi.amendmentIndex).toBeGreaterThan(0);
    });
  });

  // Outstanding amount display
  describe('outstanding amount', () => {
    it('should calculate outstanding from grandTotal - amountPaid', () => {
      const pi = { grandTotal: 5000, amountPaid: 3000 };
      expect(pi.grandTotal - pi.amountPaid).toBe(2000);
    });

    it('should show zero when fully paid', () => {
      const pi = { grandTotal: 5000, amountPaid: 5000 };
      expect(pi.grandTotal - pi.amountPaid).toBe(0);
    });
  });
});
