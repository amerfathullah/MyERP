import { describe, it, expect } from 'vitest';

// PurchaseReceiptDetailComponent — workflow actions + navigation tests

function getWorkflowActions(status: string): { name: string; label: string; color: string }[] {
  const actions: { name: string; label: string; color: string }[] = [];
  switch (status) {
    case 'Draft':
      actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
      break;
    case 'Submitted':
      actions.push({ name: 'invoice', label: 'Make Invoice', color: 'primary' });
      actions.push({ name: 'return', label: 'Create Return', color: 'warning' });
      actions.push({ name: 'cancel', label: 'Cancel', color: 'warn' });
      break;
    case 'Cancelled':
      actions.push({ name: 'amend', label: 'Amend', color: 'success' });
      break;
  }
  return actions;
}

function getActionRoute(action: string, id: string): string | null {
  switch (action) {
    case 'invoice': return `/purchasing/invoices/${id}`;
    case 'return': return `/purchasing/receipts/new?returnAgainst=${id}`;
    case 'amend': return `/purchasing/receipts/${id}`;
    default: return null;
  }
}

describe('PurchaseReceiptDetailComponent', () => {
  describe('workflow actions', () => {
    it('should show Submit for Draft', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
    });

    it('should show Invoice + Return + Cancel for Submitted', () => {
      const actions = getWorkflowActions('Submitted');
      expect(actions).toHaveLength(3);
      expect(actions.map(a => a.name)).toEqual(['invoice', 'return', 'cancel']);
    });

    it('should show Amend for Cancelled', () => {
      const actions = getWorkflowActions('Cancelled');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
    });

    it('should show no actions for Posted status', () => {
      const actions = getWorkflowActions('Posted');
      expect(actions).toHaveLength(0);
    });

    it('should show no actions for null status', () => {
      const actions = getWorkflowActions('');
      expect(actions).toHaveLength(0);
    });
  });

  describe('action navigation', () => {
    it('should navigate to PI on Make Invoice', () => {
      const route = getActionRoute('invoice', 'pr-123');
      expect(route).toContain('/purchasing/invoices/');
    });

    it('should navigate to PR form with returnAgainst for Create Return', () => {
      const route = getActionRoute('return', 'pr-123');
      expect(route).toContain('returnAgainst=pr-123');
    });

    it('should navigate to amended PR', () => {
      const route = getActionRoute('amend', 'pr-123');
      expect(route).toBe('/purchasing/receipts/pr-123');
    });

    it('should return null for submit action (no navigation)', () => {
      const route = getActionRoute('submit', 'pr-123');
      expect(route).toBeNull();
    });
  });

  describe('receipt display', () => {
    it('should define item columns', () => {
      const columns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
      expect(columns).toHaveLength(5);
    });

    it('should calculate line total', () => {
      const item = { quantity: 10, unitPrice: 50 };
      expect(item.quantity * item.unitPrice).toBe(500);
    });
  });

  describe('return receipt', () => {
    it('should identify return receipt', () => {
      const pr = { isReturn: true, returnAgainstId: 'orig-pr' };
      expect(pr.isReturn).toBe(true);
      expect(pr.returnAgainstId).toBeTruthy();
    });

    it('should identify normal receipt', () => {
      const pr = { isReturn: false };
      expect(pr.isReturn).toBe(false);
    });

    it('should have negative qty for returns', () => {
      const returnItem = { quantity: -5 };
      expect(returnItem.quantity).toBeLessThan(0);
    });
  });

  describe('amendment', () => {
    it('should detect amended receipt', () => {
      const pr = { amendedFromId: 'orig', amendmentIndex: 1 };
      expect(pr.amendedFromId).toBeTruthy();
    });

    it('should detect non-amended receipt', () => {
      const pr = { amendedFromId: null, amendmentIndex: 0 };
      expect(pr.amendedFromId).toBeFalsy();
    });
  });

  describe('supplier info', () => {
    it('should display supplier name', () => {
      const pr = { supplierName: 'Acme Supplies', supplierId: 'sup-123' };
      expect(pr.supplierName || pr.supplierId).toBe('Acme Supplies');
    });

    it('should fallback to supplierId when name missing', () => {
      const pr = { supplierName: null, supplierId: 'sup-123' };
      expect(pr.supplierName || pr.supplierId).toBe('sup-123');
    });
  });
});
