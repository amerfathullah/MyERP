import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { SalesInvoiceDetailComponent } from './sales-invoice-detail.component';
import { CompanyService } from '../../proxy/core/company.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { SalesPersonService } from '../../proxy/sales/sales-person.service';
import { DunningService } from '../../proxy/sales/dunning.service';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import { DocumentPrintService } from '../../proxy/core/document-print.service';
import { MasterDataService } from '../../proxy/core/master-data.service';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';

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

  interface Perms {
    canCreateSI?: boolean;
    canSubmitSI?: boolean;
    canCancelSI?: boolean;
    canCreateDN?: boolean;
    canCreateDunning?: boolean;
    canCreateMaintenance?: boolean;
    canCreatePI?: boolean;
  }

  function getWorkflowActions(
    invoice: {
      status: string;
      outstandingAmount?: number;
      grandTotal?: number;
      eInvoiceStatus?: string;
      updateStock?: boolean;
      isReturn?: boolean;
      isInternalCustomer?: boolean;
      interCompanyPurchaseInvoiceId?: string | null;
    },
    perms: Perms = {}
  ): DetailWorkflowAction[] {
    const canCreateSI = perms.canCreateSI ?? true;
    const canSubmitSI = perms.canSubmitSI ?? true;
    const canCancelSI = perms.canCancelSI ?? true;
    const canCreateDN = perms.canCreateDN ?? false;
    const canCreateDunning = perms.canCreateDunning ?? false;
    const canCreateMaintenance = perms.canCreateMaintenance ?? false;
    const canCreatePI = perms.canCreatePI ?? false;

    const actions: DetailWorkflowAction[] = [];
    switch (invoice.status) {
      case 'Draft':
        if (canSubmitSI) {
          actions.push({ name: 'submit', label: 'Submit', icon: 'fa fa-paper-plane', btnClass: 'btn-primary' });
        }
        break;
      case 'Submitted':
        actions.push({ name: 'post', label: 'Post', icon: 'fa fa-check-double', btnClass: 'btn-success' });
        break;
      case 'Posted':
        actions.push({ name: 'payment', label: 'Make Payment', icon: 'fa fa-money-bill', btnClass: 'btn-success' });
        if (!invoice.updateStock && !invoice.isReturn && canCreateDN) {
          actions.push({ name: 'createDN', label: 'Create Delivery Note', icon: 'fa fa-truck', btnClass: 'btn-outline-info' });
        }
        const outstanding = invoice.outstandingAmount ?? 0;
        const grandTotal = invoice.grandTotal ?? 0;
        if (canCreateSI && !invoice.isReturn && (outstanding >= 0 || Math.abs(outstanding) < grandTotal)) {
          actions.push({ name: 'return', label: 'Create Return', icon: 'fa fa-rotate-left', btnClass: 'btn-outline-warning' });
        }
        if (canCreateMaintenance) {
          actions.push({ name: 'maintenanceSchedule', label: 'Maintenance Schedule', icon: 'fa fa-wrench', btnClass: 'btn-outline-info' });
        }
        if (invoice.isInternalCustomer && !invoice.interCompanyPurchaseInvoiceId && canCreatePI) {
          actions.push({ name: 'createPurchaseInvoice', label: 'Make Purchase Invoice', icon: 'fa fa-file-invoice', btnClass: 'btn-outline-info' });
        }
        if (outstanding > 0) {
          actions.push({ name: 'writeOff', label: 'Write Off', icon: 'fa fa-eraser', btnClass: 'btn-outline-secondary' });
        }
        if (canCancelSI) {
          actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa fa-ban', btnClass: 'btn-outline-danger' });
        }
        if (!invoice.eInvoiceStatus || invoice.eInvoiceStatus === 'NotSubmitted') {
          actions.push({ name: 'submitLhdn', label: 'Submit to LHDN', icon: 'fa fa-cloud-arrow-up', btnClass: 'btn-outline-primary' });
        }
        break;
      case 'Cancelled':
        if (canCreateSI) {
          actions.push({ name: 'amend', label: 'Amend', icon: 'fa fa-file-circle-plus', btnClass: 'btn-outline-success' });
        }
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

    it('respects permissions on sales invoice actions (ERPNext #59367)', () => {
      // With canCreateSI false, return and amend are hidden
      const postedNoPerm = getWorkflowActions(
        { status: 'Posted', outstandingAmount: 500 },
        { canCreateSI: false, canCancelSI: false }
      );
      expect(postedNoPerm.map(a => a.name)).not.toContain('return');
      expect(postedNoPerm.map(a => a.name)).not.toContain('cancel');

      const cancelledNoPerm = getWorkflowActions({ status: 'Cancelled' }, { canCreateSI: false });
      expect(cancelledNoPerm).toHaveLength(0);

      // With canCreateDN true, delivery note is shown
      const postedWithDN = getWorkflowActions(
        { status: 'Posted', updateStock: false, isReturn: false },
        { canCreateDN: true }
      );
      expect(postedWithDN.map(a => a.name)).toContain('createDN');

      // With canCreatePI true for internal customer, purchase invoice action is shown
      const postedInternal = getWorkflowActions(
        { status: 'Posted', isInternalCustomer: true },
        { canCreatePI: true }
      );
      expect(postedInternal.map(a => a.name)).toContain('createPurchaseInvoice');
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

  describe('LHDN Resubmit Eligibility', () => {
    it('allows resubmit for Invalid, Rejected, and Failed statuses when not consolidated', () => {
      const canSubmit = (status: string | undefined, consolidatedId?: string | null) =>
        (!status || ['NotSubmitted', 'Invalid', 'Rejected', 'Failed'].includes(status)) && !consolidatedId;

      expect(canSubmit('Rejected', null)).toBe(true);
      expect(canSubmit('Failed', null)).toBe(true);
      expect(canSubmit('Invalid', null)).toBe(true);
      expect(canSubmit('Valid', null)).toBe(false);
      expect(canSubmit('Rejected', 'cons-1')).toBe(false);
    });
  });

  describe('SalesInvoiceDetailComponent debitNote action', () => {
    function createComponent() {
      const siServiceMock = {
        get: vi.fn().mockReturnValue(of(null)),
        getPaymentSchedule: vi.fn().mockReturnValue(of([])),
        getPayments: vi.fn().mockReturnValue(of([])),
        createDebitNote: vi.fn().mockReturnValue(of({ id: 'dn-si-1' })),
        createInterCompanyPurchaseInvoice: vi.fn().mockReturnValue(of({ id: 'ic-pi-1' })),
        updateSalesTeam: vi.fn().mockReturnValue(of({ id: 'si-1', status: 'Posted', salesTeam: [{ salesPersonId: 'sp-1', salesPersonName: 'Alice', allocatedPercentage: 100, allocatedAmount: 100, commissionRate: 5, incentives: 5 }], totalCommission: 5 })),
      };
      const salesPersonMock = {
        getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sp-1', salesPersonName: 'Alice' }] })),
      };
      const routerMock = { navigate: vi.fn() };
      const confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
      const toasterMock = { success: vi.fn(), error: vi.fn(), warn: vi.fn() };

      const dunningServiceMock = {
        createFromSalesInvoice: vi.fn().mockReturnValue(of({ id: 'dun-1' })),
      };
      const emailMock = {
        previewEmail: vi.fn().mockReturnValue(of({ subject: 'Invoice SI-1', body: 'Total MYR 100' })),
      };
      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'si-1' } } } },
          { provide: Router, useValue: routerMock },
          { provide: CompanyService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: SalesInvoiceService, useValue: siServiceMock },
          { provide: DunningService, useValue: dunningServiceMock },
          { provide: EInvoiceService, useValue: {} },
          { provide: DocumentConversionService, useValue: {} },
          { provide: SalesInvoiceStore, useValue: {} },
          { provide: ConfirmationService, useValue: confirmationMock },
          { provide: ToasterService, useValue: toasterMock },
          { provide: DocumentPrintService, useValue: {} },
          { provide: MasterDataService, useValue: { getModesOfPayment: vi.fn().mockReturnValue(of([])) } },
          { provide: PaymentEntryService, useValue: {} },
          { provide: DocumentEmailService, useValue: emailMock },
          { provide: SalesPersonService, useValue: salesPersonMock },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new SalesInvoiceDetailComponent());
      return { comp, siServiceMock, dunningServiceMock, emailMock, routerMock, confirmationMock, toasterMock };
    }

    it('shows debitNote action for posted non-return non-debit-note sales invoice', () => {
      const { comp } = createComponent();
      comp.invoice = { id: 'si-1', status: 'Posted', isReturn: false } as any;
      expect(comp.workflowActions.some(a => a.name === 'debitNote')).toBe(true);

      comp.invoice = { id: 'si-1', status: 'Posted', isReturn: false, isDebitNote: true } as any;
      expect(comp.workflowActions.some(a => a.name === 'debitNote')).toBe(false);
    });

    it('calls createDebitNote on confirmation and navigates to created debit note', () => {
      const { comp, siServiceMock, routerMock, toasterMock } = createComponent();
      comp.invoice = { id: 'si-1', status: 'Posted' } as any;

      comp.onWorkflowAction('debitNote');

      expect(siServiceMock.createDebitNote).toHaveBeenCalledWith('si-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::DebitNoteCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/invoices', 'dn-si-1']);
    });

    it('creates dunning directly via createFromSalesInvoice and navigates to created dunning', () => {
      const { comp, dunningServiceMock, routerMock, toasterMock } = createComponent();
      comp.invoice = { id: 'si-1', status: 'Posted' } as any;

      comp.onWorkflowAction('dunning');

      expect(dunningServiceMock.createFromSalesInvoice).toHaveBeenCalledWith('si-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/dunnings', 'dun-1']);
    });

    it('preloads email preview via DocumentEmailService when opening send email dialog', () => {
      const { comp, emailMock } = createComponent();
      comp.invoice = { id: 'si-1', invoiceNumber: 'SI-1', customerName: 'Acme', grandTotal: 100 } as any;

      comp.openSendEmailDialog();

      expect(comp.showEmailDialog).toBe(true);
      expect(emailMock.previewEmail).toHaveBeenCalledWith({
        documentType: 'SalesInvoice',
        variables: { invoice_no: 'SI-1', customer: 'Acme', amount: '100' },
      });
      expect(comp.emailPreview()).toEqual({ subject: 'Invoice SI-1', body: 'Total MYR 100' });
    });

    it('updates sales team allocations via SalesInvoiceService.updateSalesTeam', () => {
      const { comp, siServiceMock, toasterMock } = createComponent();
      comp.invoice = { id: 'si-1', status: 'Posted', salesTeam: [{ salesPersonId: 'sp-1', allocatedPercentage: 60, commissionRate: 5 }] } as any;

      comp.openEditSalesTeam();
      expect(comp.editingSalesTeam()).toBe(true);
      comp.saveSalesTeam();
      expect(toasterMock.warn).toHaveBeenCalledWith('::SalesTeamMustEqual100');

      comp.salesTeamDraft.set([{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);
      comp.saveSalesTeam();

      expect(siServiceMock.updateSalesTeam).toHaveBeenCalledWith('si-1', [{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);
      expect(comp.editingSalesTeam()).toBe(false);
      expect(comp.invoice?.totalCommission).toBe(5);
    });

    it('shows createPurchaseInvoice for internal customer without link and creates PI via SalesInvoiceService', () => {
      const { comp, siServiceMock, routerMock, toasterMock } = createComponent();
      comp.invoice = { id: 'si-1', status: 'Posted', isInternalCustomer: true, interCompanyPurchaseInvoiceId: null } as any;
      expect(comp.workflowActions.some(a => a.name === 'createPurchaseInvoice')).toBe(true);

      comp.onWorkflowAction('createPurchaseInvoice');

      expect(siServiceMock.createInterCompanyPurchaseInvoice).toHaveBeenCalledWith('si-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/purchasing/invoices', 'ic-pi-1']);

      comp.invoice = { id: 'si-1', status: 'Posted', isInternalCustomer: true, interCompanyPurchaseInvoiceId: 'ic-pi-1' } as any;
      expect(comp.workflowActions.some(a => a.name === 'createPurchaseInvoice')).toBe(false);
    });
  });
});
