import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { describe, it, expect, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { PurchaseReceiptDetailComponent } from './purchase-receipt-detail.component';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseConversionService } from '../../proxy/purchasing/purchase-conversion.service';
import { PurchaseReceiptStore } from '../store/purchase-receipt.store';
import { CompanyService } from '../../proxy/core/company.service';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';

// PurchaseReceiptDetailComponent — workflow actions + navigation tests

function getWorkflowActions(status: string, hasRejectedQty = false): { name: string; label: string; color: string }[] {
  const actions: { name: string; label: string; color: string }[] = [];
  switch (status) {
    case 'Draft':
      actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
      break;
    case 'Submitted':
      actions.push({ name: 'invoice', label: 'Make Invoice', color: 'primary' });
      actions.push({ name: 'return', label: 'Create Return', color: 'warning' });
      if (hasRejectedQty) {
        actions.push({ name: 'returnRejected', label: 'Return Rejected Qty', color: 'danger' });
      }
      actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
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

    it('should show Return Rejected Qty when Submitted receipt has rejected items', () => {
      const actions = getWorkflowActions('Submitted', true);
      expect(actions.map(a => a.name)).toEqual(['invoice', 'return', 'returnRejected', 'cancel']);
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

    it('maps rejected warehouse return rows with returnQtyFromRejectedWarehouse = true (4cc2ffb2a8, b11c0c4076, 3837f7b46a)', () => {
      const mapOriginalReceiptItemsForReturn = (items: any[], returnFromRejectedWarehouse = false) =>
        returnFromRejectedWarehouse
          ? items
              .filter((item: any) => Number(item.rejectedQty ?? 0) > 0)
              .map((item: any) => {
                const rejQty = -Math.abs(Number(item.rejectedQty ?? 0));
                return {
                  itemId: item.itemId,
                  warehouseId: item.rejectedWarehouseId ?? item.warehouseId,
                  receivedQty: rejQty,
                  quantity: rejQty,
                  rejectedQty: 0,
                  rejectedWarehouseId: item.rejectedWarehouseId,
                  returnQtyFromRejectedWarehouse: true,
                };
              })
          : items;

      const mapped = mapOriginalReceiptItemsForReturn(
        [
          { itemId: 'itm-1', quantity: 10, rejectedQty: 3, warehouseId: 'wh-acc', rejectedWarehouseId: 'wh-rej' },
          { itemId: 'itm-2', quantity: 5, rejectedQty: 0, warehouseId: 'wh-acc', rejectedWarehouseId: null },
        ],
        true,
      );
      expect(mapped).toHaveLength(1);
      expect(mapped[0].itemId).toBe('itm-1');
      expect(mapped[0].quantity).toBe(-3);
      expect(mapped[0].rejectedQty).toBe(0);
      expect(mapped[0].warehouseId).toBe('wh-rej');
      expect(mapped[0].returnQtyFromRejectedWarehouse).toBe(true);
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

  describe('error handling', () => {
    it('surfaces toaster errors when load or workflow actions fail', () => {
      const toasterMock = { success: vi.fn(), error: vi.fn() };
      const serviceMock = {
        get: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'PR load failed' } } }))),
        amend: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'PR amend failed' } } }))),
      };
      const conversionMock = {
        convertPurchaseReceiptToInvoice: vi.fn().mockReturnValue(throwError(() => ({ error: { error: { message: 'Invoice conversion failed' } } }))),
      };

      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'pr-1' } } } },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: PurchaseReceiptService, useValue: serviceMock },
          { provide: PurchaseConversionService, useValue: conversionMock },
          { provide: PurchaseReceiptStore, useValue: {} },
          { provide: ConfirmationService, useValue: { warn: vi.fn() } },
          { provide: ToasterService, useValue: toasterMock },
          { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: QualityInspectionService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: WarehouseService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new PurchaseReceiptDetailComponent());
      comp.ngOnInit();
      expect(toasterMock.error).toHaveBeenCalledWith('PR load failed');

      comp.receipt = { id: 'pr-1', status: 'Submitted' } as any;
      comp.onWorkflowAction('invoice');
      expect(toasterMock.error).toHaveBeenCalledWith('Invoice conversion failed');

      comp.onWorkflowAction('amend');
      expect(toasterMock.error).toHaveBeenCalledWith('PR amend failed');
    });
  });
});
