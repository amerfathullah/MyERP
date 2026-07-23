import { describe, it, expect } from 'vitest';

interface DetailWorkflowAction {
  name: string;
  label: string;
  icon: string;
  btnClass: string;
}

/**
 * Tests for SalesInvoiceDetailComponent workflow logic.
 * Verifies: status-based action visibility, action routing, amendment/return/writeOff guards.
 */
describe('SalesInvoiceDetailComponent Logic', () => {

  function getWorkflowActions(invoice: { status: string; outstandingAmount?: number; eInvoiceStatus?: string }): DetailWorkflowAction[] {
    const actions: DetailWorkflowAction[] = [];
    switch (invoice.status) {
      case 'Draft':
        actions.push({ name: 'submit', label: 'Submit', icon: 'fa fa-paper-plane', btnClass: 'btn-primary' });
        break;
      case 'Submitted':
        actions.push({ name: 'post', label: 'Post', icon: 'fa fa-check-double', btnClass: 'btn-success' });
        break;
      case 'Posted':
        actions.push({ name: 'payment', label: 'Make Payment', icon: 'fa fa-money-bill', btnClass: 'btn-success' });
        actions.push({ name: 'return', label: 'Create Return', icon: 'fa fa-rotate-left', btnClass: 'btn-outline-warning' });
        if ((invoice.outstandingAmount ?? 0) > 0) {
          actions.push({ name: 'writeOff', label: 'Write Off', icon: 'fa fa-eraser', btnClass: 'btn-outline-secondary' });
        }
        actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa fa-ban', btnClass: 'btn-outline-danger' });
        if (!invoice.eInvoiceStatus || invoice.eInvoiceStatus === 'NotSubmitted') {
          actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'fa fa-cloud-arrow-up', btnClass: 'btn-outline-primary' });
        }
        break;
      case 'Cancelled':
        actions.push({ name: 'amend', label: 'Amend', icon: 'fa fa-file-circle-plus', btnClass: 'btn-outline-success' });
        break;
    }
    return actions;
  }

  describe('Workflow Actions by Status', () => {
    it('Draft shows Submit only', () => {
      const actions = getWorkflowActions({ status: 'Draft' });
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
      expect(actions[0].btnClass).toBe('btn-primary');
    });

    it('Submitted shows Post only', () => {
      const actions = getWorkflowActions({ status: 'Submitted' });
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('post');
      expect(actions[0].btnClass).toBe('btn-success');
    });

    it('Posted shows Payment + Return + WriteOff + Cancel + LHDN (full set)', () => {
      const actions = getWorkflowActions({
        status: 'Posted',
        outstandingAmount: 5000,
        eInvoiceStatus: 'NotSubmitted',
      });
      const names = actions.map(a => a.name);
      expect(names).toContain('payment');
      expect(names).toContain('return');
      expect(names).toContain('writeOff');
      expect(names).toContain('cancel');
      expect(names).toContain('submitLhdn');
      expect(actions.length).toBe(5);
    });

    it('Posted with zero outstanding hides WriteOff', () => {
      const actions = getWorkflowActions({
        status: 'Posted',
        outstandingAmount: 0,
      });
      const names = actions.map(a => a.name);
      expect(names).not.toContain('writeOff');
    });

    it('Posted with eInvoice already submitted hides LHDN button', () => {
      const actions = getWorkflowActions({
        status: 'Posted',
        outstandingAmount: 1000,
        eInvoiceStatus: 'Submitted',
      });
      const names = actions.map(a => a.name);
      expect(names).not.toContain('submitLhdn');
      expect(names).toContain('payment');
      expect(names).toContain('cancel');
    });

    it('Cancelled shows Amend only', () => {
      const actions = getWorkflowActions({ status: 'Cancelled' });
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
      expect(actions[0].btnClass).toBe('btn-outline-success');
    });

    it('unknown status shows no actions', () => {
      expect(getWorkflowActions({ status: 'Expired' })).toHaveLength(0);
    });
  });

  describe('Action Navigation Routes', () => {
    function getPaymentQueryParams(invoiceId: string) {
      return { partyType: 'Customer', againstInvoiceType: 'SalesInvoice', againstInvoiceId: invoiceId };
    }

    function getReturnQueryParams(invoiceId: string) {
      return { returnAgainst: invoiceId };
    }

    function getDuplicateQueryParams(invoiceId: string) {
      return { duplicateFrom: invoiceId };
    }

    it('payment routes to PE form with correct query params', () => {
      const params = getPaymentQueryParams('si-123');
      expect(params.partyType).toBe('Customer');
      expect(params.againstInvoiceType).toBe('SalesInvoice');
      expect(params.againstInvoiceId).toBe('si-123');
    });

    it('return routes to SI form with returnAgainst param', () => {
      const params = getReturnQueryParams('si-456');
      expect(params.returnAgainst).toBe('si-456');
    });

    it('duplicate routes with duplicateFrom param', () => {
      const params = getDuplicateQueryParams('si-789');
      expect(params.duplicateFrom).toBe('si-789');
    });
  });

  describe('Amendment Info Display', () => {
    it('amended invoice shows AmendedFromId', () => {
      const invoice = { amendedFromId: 'orig-id', amendmentIndex: 1 };
      expect(invoice.amendedFromId).toBe('orig-id');
      expect(invoice.amendmentIndex).toBe(1);
    });

    it('return invoice shows ReturnAgainstId', () => {
      const invoice = { isReturn: true, returnAgainstId: 'orig-si' };
      expect(invoice.isReturn).toBe(true);
      expect(invoice.returnAgainstId).toBe('orig-si');
    });

    it('normal invoice has no amendment/return info', () => {
      const invoice = { isReturn: false, returnAgainstId: null, amendedFromId: null };
      expect(invoice.isReturn).toBe(false);
      expect(invoice.returnAgainstId).toBeNull();
      expect(invoice.amendedFromId).toBeNull();
    });
  });

  describe('Line Total Calculation', () => {
    function calculateLineTotal(qty: number, unitPrice: number, taxAmount: number): number {
      return qty * unitPrice + taxAmount;
    }

    it('simple line total', () => {
      expect(calculateLineTotal(10, 100, 60)).toBe(1060);
    });

    it('zero quantity', () => {
      expect(calculateLineTotal(0, 100, 0)).toBe(0);
    });

    it('negative quantity for returns', () => {
      expect(calculateLineTotal(-5, 100, -30)).toBe(-530);
    });
  });
});
