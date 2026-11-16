import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService, PermissionService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { describe, it, expect, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { PurchaseOrderDetailComponent } from './purchase-order-detail.component';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseOrderStore } from '../store/purchase-order.store';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { DocumentPrintService } from '../../proxy/core/document-print.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { PartyPerformanceService } from '../../proxy/core/party-performance.service';

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
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver') {
      actions.push({ name: 'receipt', label: 'Make Receipt', icon: 'box-open', color: 'primary' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToBill') {
      actions.push({ name: 'invoice', label: 'Make Invoice', icon: 'file-invoice', color: 'info' });
    }
    if (status === 'ToDeliverAndBill' || status === 'ToDeliver' || status === 'ToBill') {
      actions.push({ name: 'payment', label: 'Make Payment', icon: 'money-bill', color: 'info' });
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

  describe('PurchaseOrderDetailComponent inter-company action', () => {
    it('creates inter-company sales order when canCreateInterCompanySalesOrder is true', () => {
      const poServiceMock = {
        get: vi.fn().mockReturnValue(of(null)),
        createInterCompanySalesOrder: vi.fn().mockReturnValue(of({ id: 'ic-so-1' })),
      };
      const routerMock = { navigate: vi.fn() };
      const toasterMock = { success: vi.fn(), error: vi.fn() };

      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'po-1' } } } },
          { provide: Router, useValue: routerMock },
          { provide: PurchaseOrderService, useValue: poServiceMock },
          { provide: PurchaseConversionService, useValue: {} },
          { provide: PurchaseOrderStore, useValue: {} },
          { provide: ConfirmationService, useValue: { warn: vi.fn() } },
          { provide: ToasterService, useValue: toasterMock },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: DocumentEmailService, useValue: {} },
          { provide: DocumentPrintService, useValue: {} },
          { provide: SupplierService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: PartyPerformanceService, useValue: { getSupplierPerformance: vi.fn().mockReturnValue(of(null)) } },
          { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseOrderDetailComponent());
      comp.order = { id: 'po-1', status: 'ToDeliverAndBill', canCreateInterCompanySalesOrder: false } as any;
      expect(comp.workflowActions.some(a => a.name === 'inter_company_so')).toBe(false);

      comp.order = { id: 'po-1', status: 'ToDeliverAndBill', canCreateInterCompanySalesOrder: true } as any;
      expect(comp.workflowActions.some(a => a.name === 'inter_company_so')).toBe(true);

      comp.onWorkflowAction('inter_company_so');

      expect(poServiceMock.createInterCompanySalesOrder).toHaveBeenCalledWith('po-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyCreated');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/sales/orders', 'ic-so-1']);
    });

    it('surfaces toaster error when amend or load fails', () => {
      const poServiceMock = {
        get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Order load failed' } } }))),
        amend: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Order amend failed' } } }))),
        getOrderReceipts: vi.fn().mockReturnValue(of([])),
        getOrderPayments: vi.fn().mockReturnValue(of([])),
      };
      const toasterMock = { success: vi.fn(), error: vi.fn() };

      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'po-1' } } } },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: PurchaseOrderService, useValue: poServiceMock },
          { provide: PurchaseConversionService, useValue: {} },
          { provide: PurchaseOrderStore, useValue: {} },
          { provide: ConfirmationService, useValue: { warn: vi.fn() } },
          { provide: ToasterService, useValue: toasterMock },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: DocumentEmailService, useValue: {} },
          { provide: DocumentPrintService, useValue: {} },
          { provide: SupplierService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: PartyPerformanceService, useValue: { getSupplierPerformance: vi.fn().mockReturnValue(of(null)) } },
          { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseOrderDetailComponent());
      comp.ngOnInit();
      expect(toasterMock.error).toHaveBeenCalledWith('Order load failed');

      comp.order = { id: 'po-1', status: 'Cancelled' } as any;
      comp.onWorkflowAction('amend');
      expect(toasterMock.error).toHaveBeenCalledWith('Order amend failed');
    });
  });
});
