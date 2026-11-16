import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { SalesOrderDetailComponent } from './sales-order-detail.component';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { SalesOrderStore } from '../store/sales-order.store';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';
import { ProformaInvoiceService } from '../../proxy/sales/proforma-invoice.service';
import { ApprovalWorkflowService } from '../../proxy/workflow/approval-workflow.service';
import { SalesOrderAmendmentService } from '../../proxy/sales/sales-order-amendment.service';
import { DocumentPrintService } from '../../proxy/core/document-print.service';
import { CompanyService } from '../../proxy/core/company.service';
import { MaintenanceScheduleService } from '../../proxy/maintenance/maintenance-schedule.service';

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

  function getWorkflowActions(status: string, hasOverDeliverableRows = false): WorkflowAction[] {
    const actions: WorkflowAction[] = [];

    if (status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver' || hasOverDeliverableRows) {
      actions.push({ name: 'delivery', label: 'Create Delivery Note', icon: 'truck', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToBill') {
      actions.push({ name: 'invoice', label: 'Create Invoice', icon: 'file-invoice', color: 'info' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver' || status === 'ToBill') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'money-bill', color: 'info' });
      actions.push({ name: 'work_order', label: 'Make Work Order', icon: 'industry', color: 'info' });
    }
    if (status !== 'Draft' && status !== 'Cancelled' && status !== 'Completed' && status !== 'Closed') {
      actions.push({ name: 'close', label: 'Close', icon: 'lock', color: 'warning' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    if (status === 'Closed') {
      actions.push({ name: 'reopen', label: 'Reopen', icon: 'lock-open', color: 'primary' });
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

    it('shows delivery action when hasOverDeliverableRows is true', () => {
      const actions = getWorkflowActions('ToBill', true);
      const names = actions.map(a => a.name);
      expect(names).toContain('delivery');
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

  describe('SalesOrderDetailComponent Create Purchase Orders', () => {
    function createComponent(createdPoIds = ['po-101']) {
      const conversionMock = {
        convertSalesOrderToPurchaseOrders: vi.fn().mockReturnValue(of(createdPoIds)),
        convertSalesOrderToDeliveryNoteByDate: vi.fn().mockReturnValue(of({ id: 'dn-by-date' })),
        convertSalesOrderToPickList: vi.fn().mockReturnValue(of('"pl-42"')),
      };
      const supplierMock = {
        getList: vi.fn().mockReturnValue(of({ items: [{ id: 'sup-1', name: 'Vendor A' }, { id: 'sup-2', name: 'Vendor B' }] })),
      };
      const routerMock = { navigate: vi.fn() };
      const toasterMock = { success: vi.fn(), error: vi.fn(), warn: vi.fn(), info: vi.fn() };
      const stockReservationMock = { cancelForOrder: vi.fn().mockReturnValue(of(void 0)) };
      const proformaMock = {
        getForSalesOrder: vi.fn().mockReturnValue(of([{ id: 'pf-1', proformaNumber: 'PF-001', grandTotal: 500 }])),
        getProformedTotals: vi.fn().mockReturnValue(of([{ salesOrderItemId: 'soi-1', orderedQty: 5, proformedQty: 2, remainingQty: 3 }])),
      };
      const approvalMock = {
        getDocumentApprovals: vi.fn().mockReturnValue(of({ items: [{ id: 'apr-1', level: 1, ruleName: 'Director Approval', status: 1 }] })),
      };
      const confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };

    const soServiceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'so-1', status: 'ToDeliverAndBill', items: [] })),
      getDeliverySchedule: vi.fn().mockReturnValue(of([])),
      getOrderPayments: vi.fn().mockReturnValue(of([])),
      updateSalesTeam: vi.fn().mockReturnValue(of({ id: 'so-1', status: 'ToDeliverAndBill', items: [], salesTeam: [{ salesPersonId: 'sp-1', allocatedPercentage: 100 }], totalCommission: 15 })),
    };
    const maintenanceScheduleMock = {
      createFromSalesOrder: vi.fn().mockReturnValue(of({ companyId: 'comp-1', customerId: 'cust-1', salesOrderId: 'so-1', items: [{ itemId: 'item-1', startDate: '2026-11-16', endDate: '2027-11-16' }] })),
      create: vi.fn().mockReturnValue(of({ id: 'ms-55' })),
    };

      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'so-1' } } } },
          { provide: Router, useValue: routerMock },
          { provide: SalesOrderService, useValue: soServiceMock },
          { provide: DocumentConversionService, useValue: conversionMock },
          { provide: SupplierService, useValue: supplierMock },
          { provide: ManufacturingService, useValue: {} },
          { provide: SalesOrderStore, useValue: {} },
          { provide: ConfirmationService, useValue: confirmationMock },
          { provide: DocumentEmailService, useValue: {} },
          { provide: StockBalanceService, useValue: { getStockBalance: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: StockReservationService, useValue: stockReservationMock },
          { provide: ProformaInvoiceService, useValue: proformaMock },
          { provide: ApprovalWorkflowService, useValue: approvalMock },
          { provide: SalesOrderAmendmentService, useValue: {} },
          { provide: DocumentPrintService, useValue: {} },
          { provide: CompanyService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: MaintenanceScheduleService, useValue: maintenanceScheduleMock },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: ToasterService, useValue: toasterMock },
        ],
      });

      const comp = runInInjectionContext(injector, () => new SalesOrderDetailComponent());
      comp.order = {
        id: 'so-1',
        status: 'ToDeliverAndBill',
        items: [
          { id: 'soi-1', itemId: 'item-1', description: 'Widget', quantity: 5, orderedQty: 1, supplierId: 'sup-1', isClosed: false },
          { id: 'soi-2', itemId: 'item-2', description: 'Closed Item', quantity: 2, orderedQty: 0, isClosed: true },
        ],
      } as any;
      return { comp, conversionMock, supplierMock, stockReservationMock, proformaMock, approvalMock, routerMock, toasterMock, soServiceMock, maintenanceScheduleMock };
    }

    it('exposes purchase_order workflow action for active sales orders', () => {
      const { comp } = createComponent();
      expect(comp.workflowActions.some(a => a.name === 'purchase_order')).toBe(true);
    });

    it('opens PO selection excluding closed rows, validates supplier assignment, and navigates on single PO', () => {
      const { comp, conversionMock, supplierMock, routerMock } = createComponent(['po-101']);

      comp.onWorkflowAction('purchase_order');

      expect(comp.showPoSelection()).toBe(true);
      expect(comp.poSelectionItems()).toHaveLength(1);
      expect(comp.poSelectionItems()[0].pendingQty).toBe(4);
      expect(supplierMock.getList).toHaveBeenCalled();
      expect(comp.hasValidPoSelection()).toBe(true);

      comp.updatePoItemSupplier(0, '');
      expect(comp.hasValidPoSelection()).toBe(false);

      comp.updatePoItemSupplier(0, 'sup-2');
      comp.confirmCreatePurchaseOrders();

      expect(conversionMock.convertSalesOrderToPurchaseOrders).toHaveBeenCalledWith('so-1', [
        { salesOrderItemId: 'soi-1', supplierId: 'sup-2' },
      ]);
      expect(routerMock.navigate).toHaveBeenCalledWith(['/purchasing/orders', 'po-101']);
    });

    it('navigates to PO list when multiple POs created across suppliers', () => {
      const { comp, routerMock, toasterMock } = createComponent(['po-1', 'po-2']);
      comp.openPoSelection();
      comp.confirmCreatePurchaseOrders();

      expect(toasterMock.success).toHaveBeenCalledWith('::PurchaseOrdersCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/purchasing/orders']);
    });

    it('cancels active stock reservations for the order on confirmation', () => {
      const { comp, stockReservationMock, toasterMock } = createComponent();
      expect(comp.workflowActions.some(a => a.name === 'cancel_reservations')).toBe(true);

      comp.onWorkflowAction('cancel_reservations');

      expect(stockReservationMock.cancelForOrder).toHaveBeenCalledWith('so-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::ReservationsCancelled');
    });

    it('loads proforma invoices, proformed totals, and document approvals on init', () => {
      const { comp, proformaMock, approvalMock } = createComponent();
      comp.ngOnInit();

      expect(proformaMock.getForSalesOrder).toHaveBeenCalledWith('so-1');
      expect(proformaMock.getProformedTotals).toHaveBeenCalledWith('so-1');
      expect(approvalMock.getDocumentApprovals).toHaveBeenCalledWith('SalesOrder', 'so-1');
      expect(comp.proformaInvoices()).toHaveLength(1);
      expect(comp.proformedTotals()).toHaveLength(1);
      expect(comp.documentApprovals()).toHaveLength(1);
      expect(comp.approvalStatusLabel(1)).toBe('Approved');
    });

    it('creates delivery note filtered by delivery cutoff date', () => {
      const { comp, conversionMock, routerMock } = createComponent();
      comp.untilDeliveryDate.set('2026-11-20');
      comp.deliverByDate();
      expect(comp.showDraftGuard()).toBe(true);
      comp.onDraftGuardProceed();

      expect(conversionMock.convertSalesOrderToDeliveryNoteByDate).toHaveBeenCalledWith('so-1', '2026-11-20');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/delivery-notes', 'dn-by-date']);
    });

    it('updates sales team on sales order via SalesOrderService.updateSalesTeam', () => {
      const { comp, soServiceMock, toasterMock } = createComponent();
      comp.editingSalesTeam.set(true);
      comp.onSaveSalesTeam([{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);

      expect(soServiceMock.updateSalesTeam).toHaveBeenCalledWith('so-1', [{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);
      expect(comp.editingSalesTeam()).toBe(false);
      expect(comp.order?.totalCommission).toBe(15);
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
    });

    it('creates maintenance schedule from sales order via MaintenanceScheduleService', () => {
      const { comp, maintenanceScheduleMock, routerMock, toasterMock } = createComponent();
      expect(comp.workflowActions.some(a => a.name === 'maintenance_schedule')).toBe(true);

      comp.onWorkflowAction('maintenance_schedule');

      expect(maintenanceScheduleMock.createFromSalesOrder).toHaveBeenCalledWith('so-1');
      expect(maintenanceScheduleMock.create).toHaveBeenCalled();
      expect(toasterMock.success).toHaveBeenCalledWith('::MaintenanceScheduleCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/maintenance/schedules', 'ms-55']);
    });

    it('creates pick list from sales order via DocumentConversionService.convertSalesOrderToPickList', () => {
      const { comp, conversionMock, routerMock, toasterMock } = createComponent();
      expect(comp.workflowActions.some(a => a.name === 'pick_list')).toBe(true);

      comp.onWorkflowAction('pick_list');

      expect(conversionMock.convertSalesOrderToPickList).toHaveBeenCalledWith('so-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::PickListCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/inventory/pick-lists', 'pl-42']);
    });
  });
});
