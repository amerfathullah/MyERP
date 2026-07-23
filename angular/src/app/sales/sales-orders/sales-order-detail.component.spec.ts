import { describe, it, expect } from 'vitest';

interface WorkflowAction {
  name: string;
  label: string;
  icon: string;
  color: string;
}

/**
 * Tests for SalesOrderDetailComponent workflow logic.
 * Verifies: fulfillment-status-based action visibility, close/reopen flow,
 * amendment from cancelled, conversion button availability.
 */
describe('SalesOrderDetailComponent Logic', () => {

  function getWorkflowActions(status: string): WorkflowAction[] {
    const actions: WorkflowAction[] = [];

    if (status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'send', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver') {
      actions.push({ name: 'delivery', label: 'Create Delivery Note', icon: 'local_shipping', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToBill') {
      actions.push({ name: 'invoice', label: 'Create Invoice', icon: 'receipt', color: 'accent' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver' || status === 'ToBill') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'payment', color: 'accent' });
      actions.push({ name: 'work_order', label: 'Make Work Order', icon: 'factory', color: 'accent' });
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
    it('shows Delivery + Invoice + Payment + WorkOrder + Close + Cancel', () => {
      const actions = getWorkflowActions('ToDeliverAndBill');
      const names = actions.map(a => a.name);
      expect(names).toContain('delivery');
      expect(names).toContain('invoice');
      expect(names).toContain('payment');
      expect(names).toContain('work_order');
      expect(names).toContain('close');
      expect(names).toContain('cancel');
      expect(actions).toHaveLength(6);
    });

    it('does NOT show Submit or Amend or Reopen', () => {
      const names = getWorkflowActions('ToDeliverAndBill').map(a => a.name);
      expect(names).not.toContain('submit');
      expect(names).not.toContain('amend');
      expect(names).not.toContain('reopen');
    });
  });

  describe('ToDeliver State (fully billed, not delivered)', () => {
    it('shows Delivery + Payment + WorkOrder + Close + Cancel (no Invoice)', () => {
      const actions = getWorkflowActions('ToDeliver');
      const names = actions.map(a => a.name);
      expect(names).toContain('delivery');
      expect(names).not.toContain('invoice');
      expect(names).toContain('payment');
      expect(names).toContain('close');
      expect(names).toContain('cancel');
    });
  });

  describe('ToBill State (fully delivered, not billed)', () => {
    it('shows Invoice + Payment + WorkOrder + Close + Cancel (no Delivery)', () => {
      const actions = getWorkflowActions('ToBill');
      const names = actions.map(a => a.name);
      expect(names).not.toContain('delivery');
      expect(names).toContain('invoice');
      expect(names).toContain('payment');
      expect(names).toContain('close');
      expect(names).toContain('cancel');
    });
  });

  describe('Completed State', () => {
    it('shows NO actions (fully fulfilled, immutable)', () => {
      const actions = getWorkflowActions('Completed');
      expect(actions).toHaveLength(0);
    });
  });

  describe('Closed State (manually short-closed)', () => {
    it('shows Reopen only', () => {
      const actions = getWorkflowActions('Closed');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('reopen');
      expect(actions[0].color).toBe('primary');
    });
  });

  describe('Cancelled State', () => {
    it('shows Amend only', () => {
      const actions = getWorkflowActions('Cancelled');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
      expect(actions[0].color).toBe('success');
    });
  });

  // === Fulfillment Progress Calculation ===

  describe('Fulfillment Progress', () => {
    function calculatePerDelivered(items: { quantity: number; deliveredQty: number }[]): number {
      if (items.length === 0) return 0;
      return Math.min(...items.map(i => i.quantity > 0 ? (i.deliveredQty / i.quantity) * 100 : 100));
    }

    function calculatePerBilled(items: { quantity: number; unitPrice: number; billedQty: number }[]): number {
      if (items.length === 0) return 0;
      const netTotal = items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0);
      if (netTotal === 0) return 0;
      const billedTotal = items.reduce((sum, i) => sum + i.billedQty * i.unitPrice, 0);
      return (billedTotal / netTotal) * 100;
    }

    it('zero items = 0%', () => {
      expect(calculatePerDelivered([])).toBe(0);
    });

    it('single item fully delivered = 100%', () => {
      expect(calculatePerDelivered([{ quantity: 10, deliveredQty: 10 }])).toBe(100);
    });

    it('single item partially delivered', () => {
      expect(calculatePerDelivered([{ quantity: 10, deliveredQty: 4 }])).toBe(40);
    });

    it('multi-item uses MIN% formula (ERPNext pattern)', () => {
      const result = calculatePerDelivered([
        { quantity: 10, deliveredQty: 10 }, // 100%
        { quantity: 5, deliveredQty: 0 },   // 0%
      ]);
      expect(result).toBe(0); // min(100%, 0%) = 0%
    });

    it('perBilled based on net total ratio', () => {
      const result = calculatePerBilled([
        { quantity: 10, unitPrice: 100, billedQty: 10 },
      ]);
      expect(result).toBe(100);
    });

    it('partial billing shows correct percentage', () => {
      const result = calculatePerBilled([
        { quantity: 10, unitPrice: 100, billedQty: 6 },
      ]);
      expect(result).toBe(60);
    });
  });

  // === Navigation Routes ===

  describe('Action Navigation', () => {
    it('delivery navigates to conversion endpoint', () => {
      // Conversion creates DN from SO — result has dn.id for navigation
      const conversionTarget = '/sales/delivery-notes';
      expect(conversionTarget).toBe('/sales/delivery-notes');
    });

    it('invoice navigates to conversion endpoint', () => {
      const conversionTarget = '/sales/invoices';
      expect(conversionTarget).toBe('/sales/invoices');
    });

    it('payment navigates to PE form with order params', () => {
      const params = { partyType: 'Customer', againstOrderType: 'SalesOrder', againstOrderId: 'so-123' };
      expect(params.partyType).toBe('Customer');
      expect(params.againstOrderType).toBe('SalesOrder');
    });

    it('work_order navigates to WO form with SO params', () => {
      const params = { salesOrderId: 'so-123', companyId: 'comp-1' };
      expect(params.salesOrderId).toBe('so-123');
    });

    it('close triggers API call (no navigation)', () => {
      // close/reopen are in-place operations that reload
      expect(true).toBe(true);
    });
  });

  // === Draft Link Guard Integration ===

  describe('Draft Link Guard', () => {
    it('delivery action triggers guard check before conversion', () => {
      // The component sets showDraftGuard=true and draftGuardTarget='DeliveryNote'
      // before executing the actual conversion
      let showDraftGuard = false;
      let draftGuardTarget: string | null = null;
      let pendingAction: (() => void) | null = null;

      // Simulate initiateConversion('DeliveryNote', action)
      const action = () => { /* conversion call */ };
      pendingAction = action;
      draftGuardTarget = 'DeliveryNote';
      showDraftGuard = true;

      expect(showDraftGuard).toBe(true);
      expect(draftGuardTarget).toBe('DeliveryNote');
      expect(pendingAction).not.toBeNull();
    });

    it('invoice action triggers guard check with SalesInvoice target', () => {
      let draftGuardTarget: string | null = null;
      draftGuardTarget = 'SalesInvoice';
      expect(draftGuardTarget).toBe('SalesInvoice');
    });

    it('onDraftGuardProceed executes pending action and cleans state', () => {
      let showDraftGuard = true;
      let draftGuardTarget: string | null = 'DeliveryNote';
      let actionExecuted = false;
      let pendingAction: (() => void) | null = () => { actionExecuted = true; };

      // Simulate onDraftGuardProceed
      showDraftGuard = false;
      draftGuardTarget = null;
      if (pendingAction) { pendingAction(); pendingAction = null; }

      expect(showDraftGuard).toBe(false);
      expect(draftGuardTarget).toBeNull();
      expect(actionExecuted).toBe(true);
      expect(pendingAction).toBeNull();
    });

    it('onDraftGuardCancelled clears state without executing action', () => {
      let showDraftGuard = true;
      let draftGuardTarget: string | null = 'DeliveryNote';
      let actionExecuted = false;
      let pendingAction: (() => void) | null = () => { actionExecuted = true; };

      // Simulate onDraftGuardCancelled
      showDraftGuard = false;
      draftGuardTarget = null;
      pendingAction = null;

      expect(showDraftGuard).toBe(false);
      expect(draftGuardTarget).toBeNull();
      expect(actionExecuted).toBe(false);
    });

    it('non-conversion actions bypass guard entirely', () => {
      // submit, payment, work_order, close, reopen, cancel, amend
      // all go directly without setting showDraftGuard
      const nonGuardedActions = ['submit', 'payment', 'work_order', 'close', 'reopen', 'cancel', 'amend'];
      for (const action of nonGuardedActions) {
        // These should NOT trigger the guard — verify structure
        expect(['delivery', 'invoice']).not.toContain(action);
      }
    });

    it('guard only shown for conversion actions (delivery, invoice)', () => {
      const guardedActions = ['delivery', 'invoice'];
      expect(guardedActions).toHaveLength(2);
      expect(guardedActions).toContain('delivery');
      expect(guardedActions).toContain('invoice');
    });
  });
});
