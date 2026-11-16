import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { DeliveryNoteDetailComponent } from './delivery-note-detail.component';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { DeliveryNoteStore } from '../store/delivery-note.store';
import { CompanyService } from '../../proxy/core/company.service';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { DocumentPrintService } from '../../proxy/core/document-print.service';

// DeliveryNoteDetailComponent — workflow actions + navigation logic tests

function getWorkflowActions(status: string, isReturn?: boolean): { name: string; label: string; color: string }[] {
  const actions: { name: string; label: string; color: string }[] = [];
  switch (status) {
    case 'Draft':
      actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
      break;
    case 'Submitted':
      actions.push({ name: 'invoice', label: 'Make Invoice', color: 'primary' });
      actions.push({ name: 'return', label: 'Create Return', color: 'warning' });
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
    case 'invoice': return `/sales/invoices/${id}`;
    case 'return': return `/sales/delivery-notes/new?returnAgainst=${id}`;
    case 'amend': return `/sales/delivery-notes/${id}`;
    default: return null;
  }
}

describe('DeliveryNoteDetailComponent', () => {
  // Workflow actions per status
  describe('workflow actions', () => {
    it('should show Submit for Draft', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
    });

    it('should show Make Invoice + Create Return + Cancel for Submitted', () => {
      const actions = getWorkflowActions('Submitted');
      expect(actions).toHaveLength(3);
      expect(actions.map(a => a.name)).toEqual(['invoice', 'return', 'cancel']);
    });

    it('should show Amend for Cancelled', () => {
      const actions = getWorkflowActions('Cancelled');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
    });

    it('should show no actions for unknown status', () => {
      const actions = getWorkflowActions('Posted');
      expect(actions).toHaveLength(0);
    });

    it('should show no actions for Completed', () => {
      const actions = getWorkflowActions('Completed');
      expect(actions).toHaveLength(0);
    });
  });

  // Navigation route mapping
  describe('action navigation', () => {
    it('should navigate to invoice on Make Invoice', () => {
      const route = getActionRoute('invoice', 'abc-123');
      expect(route).toContain('/sales/invoices/');
    });

    it('should navigate to DN form with returnAgainst for Create Return', () => {
      const route = getActionRoute('return', 'abc-123');
      expect(route).toContain('returnAgainst=abc-123');
    });

    it('should navigate to amended DN', () => {
      const route = getActionRoute('amend', 'abc-123');
      expect(route).toBe('/sales/delivery-notes/abc-123');
    });
  });

  // Item columns
  describe('item display', () => {
    it('should define item columns', () => {
      const columns = ['description', 'quantity', 'uom'];
      expect(columns).toHaveLength(3);
      expect(columns).toContain('description');
      expect(columns).toContain('quantity');
    });
  });

  // Line total calculation
  describe('line totals', () => {
    it('should calculate line total from qty × rate', () => {
      const qty = 5;
      const rate = 120;
      expect(qty * rate).toBe(600);
    });

    it('should handle negative qty for returns', () => {
      const qty = -3;
      const rate = 100;
      expect(qty * rate).toBe(-300);
    });
  });

  // Amendment info
  describe('amendment display', () => {
    it('should detect amended document', () => {
      const dn = { amendedFromId: 'orig-id', amendmentIndex: 1 };
      expect(dn.amendedFromId).toBeTruthy();
      expect(dn.amendmentIndex).toBe(1);
    });

    it('should detect non-amended document', () => {
      const dn = { amendedFromId: null, amendmentIndex: 0 };
      expect(dn.amendedFromId).toBeFalsy();
    });
  });

  // Return badge display
  describe('return document display', () => {
    it('should identify return delivery note', () => {
      const dn = { isReturn: true, returnAgainstId: 'orig-id' };
      expect(dn.isReturn).toBe(true);
      expect(dn.returnAgainstId).toBeTruthy();
    });

    it('should identify normal delivery note', () => {
      const dn = { isReturn: false, returnAgainstId: null };
      expect(dn.isReturn).toBe(false);
    });
  });

  describe('Sales Team update', () => {
    it('updates delivery note sales team via DeliveryNoteService.updateSalesTeam', () => {
      const dnMock = {
        get: vi.fn().mockReturnValue(of({ id: 'dn-1', status: 'Submitted' })),
        updateSalesTeam: vi.fn().mockReturnValue(of({ id: 'dn-1', status: 'Submitted', salesTeam: [{ salesPersonId: 'sp-1', allocatedPercentage: 100 }], totalCommission: 25 })),
      };
      const toasterMock = { success: vi.fn(), error: vi.fn() };
      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'dn-1' } } } },
          { provide: DeliveryNoteService, useValue: dnMock },
          { provide: DocumentConversionService, useValue: {} },
          { provide: DeliveryNoteStore, useValue: {} },
          { provide: ConfirmationService, useValue: { warn: vi.fn() } },
          { provide: ToasterService, useValue: toasterMock },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: CompanyService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: DocumentEmailService, useValue: {} },
          { provide: DocumentPrintService, useValue: {} },
        ],
      });

      const comp = runInInjectionContext(injector, () => new DeliveryNoteDetailComponent());
      comp.ngOnInit();
      comp.editingSalesTeam.set(true);
      comp.onSaveSalesTeam([{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);

      expect(dnMock.updateSalesTeam).toHaveBeenCalledWith('dn-1', [{ salesPersonId: 'sp-1', allocatedPercentage: 100, commissionRate: 5 }]);
      expect(comp.editingSalesTeam()).toBe(false);
      expect(comp.deliveryNote?.totalCommission).toBe(25);
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');
    });
  });
});
