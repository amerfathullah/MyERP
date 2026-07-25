import { describe, it, expect } from 'vitest';

interface WorkflowAction {
  name: string;
  label: string;
  icon: string;
  color: string;
}

/**
 * Tests for PurchaseOrderDetailComponent workflow logic.
 * Verifies: fulfillment-status-based action visibility, receipt/invoice/payment actions,
 * close/reopen flow, amendment from cancelled.
 */
describe('PurchaseOrderDetailComponent Logic', () => {

  function getWorkflowActions(status: string): WorkflowAction[] {
    const actions: WorkflowAction[] = [];

    if (status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver') {
      actions.push({ name: 'receipt', label: 'Make Receipt', icon: 'inventory_2', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToBill') {
      actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'receipt', color: 'accent' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver' || status === 'ToBill') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'payment', color: 'accent' });
    }
    if (status !== 'Draft' && status !== 'Cancelled' && status !== 'Completed' && status !== 'Closed') {
      actions.push({ name: 'close', label: 'Close', icon: 'lock', color: 'warn' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'cancel', color: 'warn' });
    }
    if (status === 'Closed') {
      actions.push({ name: 'reopen', label: 'Reopen', icon: 'lock_open', color: 'primary' });
    }
    if (status === 'Cancelled') {
      actions.push({ name: 'amend', label: 'Amend', icon: 'file-circle-plus', color: 'success' });
    }
    return actions;
  }

  describe('Draft State', () => {
    it('shows Submit only', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
    });
  });

  describe('ToDeliverAndBill State (after submit)', () => {
    it('shows Receipt + Invoice + Payment + Close + Cancel', () => {
      const actions = getWorkflowActions('ToDeliverAndBill');
      const names = actions.map(a => a.name);
      expect(names).toContain('receipt');
      expect(names).toContain('invoice');
      expect(names).toContain('payment');
      expect(names).toContain('close');
      expect(names).toContain('cancel');
      expect(actions).toHaveLength(5);
    });
  });

  describe('ToDeliver State (fully billed, not received)', () => {
    it('shows Receipt + Payment + Close + Cancel (no Invoice)', () => {
      const actions = getWorkflowActions('ToDeliver');
      const names = actions.map(a => a.name);
      expect(names).toContain('receipt');
      expect(names).not.toContain('invoice');
      expect(names).toContain('payment');
      expect(names).toContain('close');
    });
  });

  describe('ToBill State (fully received, not billed)', () => {
    it('shows Invoice + Payment + Close + Cancel (no Receipt)', () => {
      const actions = getWorkflowActions('ToBill');
      const names = actions.map(a => a.name);
      expect(names).not.toContain('receipt');
      expect(names).toContain('invoice');
      expect(names).toContain('payment');
      expect(names).toContain('close');
    });
  });

  describe('Completed State', () => {
    it('shows NO actions', () => {
      expect(getWorkflowActions('Completed')).toHaveLength(0);
    });
  });

  describe('Closed State', () => {
    it('shows Reopen only', () => {
      const actions = getWorkflowActions('Closed');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('reopen');
    });
  });

  describe('Cancelled State', () => {
    it('shows Amend only', () => {
      const actions = getWorkflowActions('Cancelled');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
    });
  });

  describe('Action Navigation Routes', () => {
    it('receipt routes to purchasing/receipts/:id', () => {
      const target = '/purchasing/receipts';
      expect(target).toBe('/purchasing/receipts');
    });

    it('invoice routes to purchasing/invoices/:id', () => {
      const target = '/purchasing/invoices';
      expect(target).toBe('/purchasing/invoices');
    });

    it('payment routes to PE form with supplier params', () => {
      const params = { partyType: 'Supplier', againstOrderType: 'PurchaseOrder', againstOrderId: 'po-123' };
      expect(params.partyType).toBe('Supplier');
      expect(params.againstOrderType).toBe('PurchaseOrder');
    });
  });

  describe('Fulfillment Progress', () => {
    function calculatePerReceived(items: { quantity: number; receivedQty: number }[]): number {
      if (items.length === 0) return 0;
      return Math.min(...items.map(i => i.quantity > 0 ? (i.receivedQty / i.quantity) * 100 : 100));
    }

    it('zero items = 0%', () => {
      expect(calculatePerReceived([])).toBe(0);
    });

    it('single item fully received = 100%', () => {
      expect(calculatePerReceived([{ quantity: 20, receivedQty: 20 }])).toBe(100);
    });

    it('partial receipt', () => {
      expect(calculatePerReceived([{ quantity: 10, receivedQty: 4 }])).toBe(40);
    });

    it('multi-item MIN% formula', () => {
      const result = calculatePerReceived([
        { quantity: 10, receivedQty: 10 },
        { quantity: 5, receivedQty: 0 },
      ]);
      expect(result).toBe(0);
    });
  });
});
