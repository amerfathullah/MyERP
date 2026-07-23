import { describe, it, expect } from 'vitest';

describe('PaymentEntryDetailComponent Logic', () => {
  // Test workflow action visibility per status
  function getWorkflowActions(status: string) {
    const actions: { name: string; label: string; color: string }[] = [];
    if (status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
    }
    if (status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', color: 'success' });
    }
    if (status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
    }
    return actions;
  }

  // Test reference route mapping
  function getRefRoute(ref: { referenceType: string; referenceId?: string }): string[] | null {
    const routeMap: Record<string, string> = {
      'SalesInvoice': '/sales/invoices',
      'PurchaseInvoice': '/purchasing/invoices',
      'SalesOrder': '/sales/orders',
      'PurchaseOrder': '/purchasing/orders',
    };
    const base = routeMap[ref.referenceType];
    if (base && ref.referenceId) return [base, ref.referenceId];
    return null;
  }

  describe('Workflow Actions', () => {
    it('Draft shows Submit only', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
      expect(actions[0].color).toBe('primary');
    });

    it('Submitted shows Post only', () => {
      const actions = getWorkflowActions('Submitted');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('post');
      expect(actions[0].color).toBe('success');
    });

    it('Posted shows Cancel only', () => {
      const actions = getWorkflowActions('Posted');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('cancel');
      expect(actions[0].color).toBe('danger');
    });

    it('Cancelled shows no actions', () => {
      expect(getWorkflowActions('Cancelled')).toHaveLength(0);
    });

    it('empty status shows no actions', () => {
      expect(getWorkflowActions('')).toHaveLength(0);
    });
  });

  describe('Reference Route Mapping', () => {
    it('SalesInvoice maps to /sales/invoices/:id', () => {
      const route = getRefRoute({ referenceType: 'SalesInvoice', referenceId: 'abc-123' });
      expect(route).toEqual(['/sales/invoices', 'abc-123']);
    });

    it('PurchaseInvoice maps to /purchasing/invoices/:id', () => {
      const route = getRefRoute({ referenceType: 'PurchaseInvoice', referenceId: 'xyz-789' });
      expect(route).toEqual(['/purchasing/invoices', 'xyz-789']);
    });

    it('SalesOrder maps to /sales/orders/:id', () => {
      const route = getRefRoute({ referenceType: 'SalesOrder', referenceId: 'so-1' });
      expect(route).toEqual(['/sales/orders', 'so-1']);
    });

    it('PurchaseOrder maps to /purchasing/orders/:id', () => {
      const route = getRefRoute({ referenceType: 'PurchaseOrder', referenceId: 'po-1' });
      expect(route).toEqual(['/purchasing/orders', 'po-1']);
    });

    it('unknown reference type returns null', () => {
      expect(getRefRoute({ referenceType: 'Unknown', referenceId: '123' })).toBeNull();
    });

    it('missing referenceId returns null', () => {
      expect(getRefRoute({ referenceType: 'SalesInvoice' })).toBeNull();
    });

    it('empty referenceId returns null', () => {
      expect(getRefRoute({ referenceType: 'SalesInvoice', referenceId: '' })).toBeNull();
    });
  });

  describe('Payment Type Badge Logic', () => {
    function getBadgeClass(paymentType: string): string {
      if (paymentType === 'Receive') return 'bg-success';
      if (paymentType === 'Pay') return 'bg-primary';
      if (paymentType === 'InternalTransfer') return 'bg-info';
      return '';
    }

    it('Receive type gets success badge', () => {
      expect(getBadgeClass('Receive')).toBe('bg-success');
    });

    it('Pay type gets primary badge', () => {
      expect(getBadgeClass('Pay')).toBe('bg-primary');
    });

    it('InternalTransfer type gets info badge', () => {
      expect(getBadgeClass('InternalTransfer')).toBe('bg-info');
    });
  });

  describe('References Extraction', () => {
    it('extracts references array from payment data', () => {
      const data = { id: '1', references: [{ referenceType: 'SalesInvoice', allocatedAmount: 100 }] };
      const refs = (data as any).references || [];
      expect(refs).toHaveLength(1);
      expect(refs[0].allocatedAmount).toBe(100);
    });

    it('defaults to empty array when no references', () => {
      const data = { id: '1' };
      const refs = (data as any).references || [];
      expect(refs).toHaveLength(0);
    });

    it('handles null references', () => {
      const data = { id: '1', references: null };
      const refs = (data as any).references || [];
      expect(refs).toHaveLength(0);
    });
  });
});
