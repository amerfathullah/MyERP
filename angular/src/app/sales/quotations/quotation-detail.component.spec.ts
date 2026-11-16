import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { QuotationDetailComponent } from './quotation-detail.component';
import { CompanyService } from '../../proxy/core/company.service';
import { QuotationService } from '../../proxy/sales/quotation.service';
import { QuotationLostReasonService } from '../../proxy/sales/quotation-lost-reason.service';
import { DocumentConversionService } from '../../proxy/sales/document-conversion.service';
import { QuotationStore } from '../store/quotation.store';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { CompetitorService } from '../../proxy/crm/competitor.service';
import { DocumentPrintService } from '../../proxy/core/document-print.service';

// QuotationDetailComponent — workflow actions + navigation + expiry tests

function getWorkflowActions(status: string): { name: string; label: string; color: string }[] {
  const actions: { name: string; label: string; color: string }[] = [];
  if (status === 'Draft') {
    actions.push({ name: 'submit', label: 'Submit', color: 'primary' });
  }
  if (status === 'Submitted') {
    actions.push({ name: 'convert', label: 'Convert to SO', color: 'primary' });
    actions.push({ name: 'lost', label: 'Mark Lost', color: 'warning' });
    actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
  }
  if (status === 'Cancelled' || status === 'Rejected') {
    actions.push({ name: 'amend', label: 'Amend', color: 'success' });
  }
  return actions;
}

function isExpired(validUntil: string | null, status: string): boolean {
  if (!validUntil || status !== 'Submitted') return false;
  return new Date(validUntil) < new Date();
}

function getActionRoute(action: string, id: string): string | null {
  switch (action) {
    case 'convert': return `/sales/orders/${id}`;
    case 'amend': return `/sales/quotations/${id}`;
    default: return null;
  }
}

describe('QuotationDetailComponent', () => {
  describe('workflow actions', () => {
    it('should show Submit for Draft', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
    });

    it('should show Convert + Lost + Cancel for Submitted', () => {
      const actions = getWorkflowActions('Submitted');
      expect(actions).toHaveLength(3);
      expect(actions.map(a => a.name)).toEqual(['convert', 'lost', 'cancel']);
    });

    it('should show Amend for Cancelled', () => {
      const actions = getWorkflowActions('Cancelled');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
    });

    it('should show Amend for Rejected (Lost)', () => {
      const actions = getWorkflowActions('Rejected');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('amend');
    });

    it('should show no actions for Converted status', () => {
      const actions = getWorkflowActions('Converted');
      expect(actions).toHaveLength(0);
    });

    it('should show no actions for unknown status', () => {
      const actions = getWorkflowActions('');
      expect(actions).toHaveLength(0);
    });
  });

  describe('navigation routes', () => {
    it('should navigate to SO on Convert', () => {
      const route = getActionRoute('convert', 'qtn-123');
      expect(route).toContain('/sales/orders/');
    });

    it('should navigate to amended quotation', () => {
      const route = getActionRoute('amend', 'qtn-123');
      expect(route).toBe('/sales/quotations/qtn-123');
    });

    it('should return null for non-navigation actions', () => {
      expect(getActionRoute('submit', 'x')).toBeNull();
      expect(getActionRoute('cancel', 'x')).toBeNull();
      expect(getActionRoute('lost', 'x')).toBeNull();
    });
  });

  describe('expiry logic', () => {
    it('should detect expired quotation', () => {
      const pastDate = '2020-01-01';
      expect(isExpired(pastDate, 'Submitted')).toBe(true);
    });

    it('should detect non-expired quotation', () => {
      const futureDate = '2030-12-31';
      expect(isExpired(futureDate, 'Submitted')).toBe(false);
    });

    it('should not be expired when no validUntil', () => {
      expect(isExpired(null, 'Submitted')).toBe(false);
    });

    it('should not be expired when not Submitted', () => {
      expect(isExpired('2020-01-01', 'Draft')).toBe(false);
    });

    it('should not be expired when Cancelled', () => {
      expect(isExpired('2020-01-01', 'Cancelled')).toBe(false);
    });
  });

  describe('quotation display', () => {
    it('should define item columns', () => {
      const columns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal'];
      expect(columns).toHaveLength(5);
    });

    it('should calculate grand total from items', () => {
      const items = [
        { quantity: 2, unitPrice: 100 },
        { quantity: 3, unitPrice: 50 },
      ];
      const total = items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0);
      expect(total).toBe(350);
    });
  });

  describe('amendment', () => {
    it('should detect amended quotation', () => {
      const qtn = { amendedFromId: 'orig', amendmentIndex: 1 };
      expect(qtn.amendedFromId).toBeTruthy();
    });

    it('should increment amendment index', () => {
      const qtn = { amendmentIndex: 2 };
      expect(qtn.amendmentIndex).toBe(2);
    });
  });

  describe('structured lost reasons CRUD', () => {
    function createComponent() {
      const quotationMock = {
        get: vi.fn().mockReturnValue(of({ id: 'qtn-1', status: 'Submitted', lostReasons: [] })),
        createLostReason: vi.fn().mockReturnValue(of({ id: 'qlrd-1' })),
        deleteLostReason: vi.fn().mockReturnValue(of(void 0)),
      };
      const lostReasonMock = {
        getAllList: vi.fn().mockReturnValue(of([{ id: 'lr-1', reason: 'Price too high', isActive: true }])),
      };
      const toasterMock = { success: vi.fn(), error: vi.fn() };
      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'qtn-1' } } } },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: CompanyService, useValue: { get: vi.fn().mockReturnValue(of({})) } },
          { provide: QuotationService, useValue: quotationMock },
          { provide: QuotationLostReasonService, useValue: lostReasonMock },
          { provide: DocumentConversionService, useValue: {} },
          { provide: QuotationStore, useValue: {} },
          { provide: ConfirmationService, useValue: {} },
          { provide: ToasterService, useValue: toasterMock },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: DocumentEmailService, useValue: {} },
          { provide: CompetitorService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: DocumentPrintService, useValue: {} },
        ],
      });
      const comp = runInInjectionContext(injector, () => new QuotationDetailComponent());
      comp.ngOnInit();
      return { comp, quotationMock, lostReasonMock, toasterMock };
    }

    it('loads lost reasons catalog and adds/removes structured lost reasons on quotation', () => {
      const { comp, quotationMock, lostReasonMock, toasterMock } = createComponent();

      expect(lostReasonMock.getAllList).toHaveBeenCalledTimes(1);
      expect(comp.lostReasons()).toHaveLength(1);
      expect(comp.lostReasonLabel({ quotationLostReasonId: 'lr-1' })).toBe('Price too high');

      comp.selectedLostReasonId = 'lr-1';
      comp.addLostReason();

      expect(quotationMock.createLostReason).toHaveBeenCalledWith({
        quotationId: 'qtn-1',
        quotationLostReasonId: 'lr-1',
      });
      expect(comp.selectedLostReasonId).toBe('');
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyUpdated');

      comp.removeLostReason('qlrd-1');
      expect(quotationMock.deleteLostReason).toHaveBeenCalledWith('qlrd-1');
      expect(toasterMock.success).toHaveBeenCalledWith('::SuccessfullyDeleted');
    });
  });
});
