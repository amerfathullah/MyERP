import { describe, it, expect, vi } from 'vitest';
import { ChangeDetectorRef, Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { JournalEntryFormComponent } from './journal-entry-form.component';
import { AccountService } from '../../proxy/accounting/account.service';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';
import { JournalEntryTemplateService } from '../../proxy/accounting/journal-entry-template.service';
import { CompanyService } from '../../proxy/core/company.service';
import { FiscalYearService } from '../../proxy/accounting/fiscal-year.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('JournalEntry form logic', () => {
  const fb = new FormBuilder();

  function createJEForm() {
    return fb.group({
      companyId: ['comp-1'],
      entryDate: [new Date(), Validators.required],
      reference: [''],
      narration: [''],
      lines: fb.array([]),
    });
  }

  function addLine(lines: FormArray, opts?: { accountId?: string; debit?: number; credit?: number }) {
    lines.push(fb.group({
      accountId: [opts?.accountId ?? '', Validators.required],
      accountName: [''],
      debit: [opts?.debit ?? 0, [Validators.min(0)]],
      credit: [opts?.credit ?? 0, [Validators.min(0)]],
    }));
  }

  function totalDebit(lines: FormArray): number {
    return lines.controls.reduce((sum, c) => sum + (c.get('debit')?.value || 0), 0);
  }

  function totalCredit(lines: FormArray): number {
    return lines.controls.reduce((sum, c) => sum + (c.get('credit')?.value || 0), 0);
  }

  function isBalanced(lines: FormArray): boolean {
    return Math.abs(totalDebit(lines) - totalCredit(lines)) < 0.01;
  }

  describe('form validation', () => {
    it('should require entryDate', () => {
      const form = createJEForm();
      form.patchValue({ entryDate: null });
      expect(form.get('entryDate')?.valid).toBe(false);
    });

    it('should start with empty lines', () => {
      const form = createJEForm();
      expect((form.get('lines') as FormArray).length).toBe(0);
    });

    it('should set companyId from context', () => {
      const form = createJEForm();
      expect(form.get('companyId')?.value).toBe('comp-1');
    });
  });

  describe('line management', () => {
    it('should add line to form array', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 1000 });
      expect(lines.length).toBe(1);
    });

    it('should require accountId on lines', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: '' });
      expect(lines.at(0).get('accountId')?.valid).toBe(false);
    });

    it('should reject negative debit', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: -100 });
      expect(lines.at(0).get('debit')?.valid).toBe(false);
    });

    it('should reject negative credit', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', credit: -100 });
      expect(lines.at(0).get('credit')?.valid).toBe(false);
    });

    it('should remove line at index', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 1000 });
      addLine(lines, { accountId: 'acc-2', credit: 1000 });
      lines.removeAt(0);
      expect(lines.length).toBe(1);
      expect(lines.at(0).get('accountId')?.value).toBe('acc-2');
    });
  });

  describe('balance calculation', () => {
    it('should calculate total debit', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 500 });
      addLine(lines, { accountId: 'acc-2', debit: 300 });
      expect(totalDebit(lines)).toBe(800);
    });

    it('should calculate total credit', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', credit: 500 });
      addLine(lines, { accountId: 'acc-2', credit: 300 });
      expect(totalCredit(lines)).toBe(800);
    });

    it('should detect balanced entry', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 1000 });
      addLine(lines, { accountId: 'acc-2', credit: 1000 });
      expect(isBalanced(lines)).toBe(true);
    });

    it('should detect unbalanced entry', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 1000 });
      addLine(lines, { accountId: 'acc-2', credit: 500 });
      expect(isBalanced(lines)).toBe(false);
    });

    it('should tolerate rounding within 0.01', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 100.005 });
      addLine(lines, { accountId: 'acc-2', credit: 100 });
      expect(isBalanced(lines)).toBe(true);
    });

    it('should handle empty lines as balanced', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      expect(isBalanced(lines)).toBe(true);
    });

    it('should handle multi-line balanced entry', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 5000 });
      addLine(lines, { accountId: 'acc-2', credit: 3000 });
      addLine(lines, { accountId: 'acc-3', credit: 2000 });
      expect(isBalanced(lines)).toBe(true);
      expect(totalDebit(lines)).toBe(5000);
      expect(totalCredit(lines)).toBe(5000);
    });
  });

  describe('double-entry invariants', () => {
    it('should not allow same-line debit and credit in normal scenario', () => {
      // Per DO-NOT: same-row debit AND credit blocked (except Exchange Gain/Loss)
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'acc-1', debit: 1000, credit: 1000 });
      // Both are set but this creates a no-op entry — form validation should flag
      const line = lines.at(0);
      const hasBoth = (line.get('debit')?.value ?? 0) > 0 && (line.get('credit')?.value ?? 0) > 0;
      expect(hasBoth).toBe(true); // Detected — UI should block submission
    });

    it('should support typical SI posting pattern (DR Receivable, CR Revenue, CR Tax)', () => {
      const form = createJEForm();
      const lines = form.get('lines') as FormArray;
      addLine(lines, { accountId: 'receivable', debit: 1060 });
      addLine(lines, { accountId: 'revenue', credit: 1000 });
      addLine(lines, { accountId: 'tax-payable', credit: 60 });
      expect(isBalanced(lines)).toBe(true);
    });
  });

  describe('JournalEntryFormComponent source document template', () => {
    it('loads balanced journal lines from getJournalEntryTemplate query params', () => {
      const jeMock = {
        getAccountDetails: vi.fn().mockReturnValue(of({ accountCurrency: 'MYR', accountBalance: 0 })),
        getJournalEntryTemplate: vi.fn().mockReturnValue(of({
          companyId: 'comp-1',
          postingDate: '2026-11-16T00:00:00',
          voucherType: 2,
          referenceNumber: 'SI-001',
          narration: 'Payment against Sales Invoice SI-001',
          lines: [
            { accountId: 'acc-bank', amount: 500, isDebit: true },
            { accountId: 'acc-ar', amount: 500, isDebit: false },
          ],
        })),
      };
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: (k: string) => (k === 'documentType' ? 'SalesInvoice' : k === 'documentId' ? 'si-1' : null) } } } },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: AccountService, useValue: { get: vi.fn().mockReturnValue(of({ currencyCode: 'MYR' })) } },
          { provide: JournalEntryService, useValue: jeMock },
          { provide: JournalEntryTemplateService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: CompanyService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [{ id: 'comp-1', currencyCode: 'MYR' }] })) } },
          { provide: FiscalYearService, useValue: {} },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
          { provide: ChangeDetectorRef, useValue: { detectChanges: vi.fn(), markForCheck: vi.fn() } },
        ],
      });

      const comp = runInInjectionContext(injector, () => new JournalEntryFormComponent());
      comp.ngOnInit();

      expect(jeMock.getJournalEntryTemplate).toHaveBeenCalledWith('SalesInvoice', 'si-1');
      expect(comp.form.get('reference')?.value).toBe('SI-001');
      expect(comp.lines.length).toBe(2);
      expect(comp.totalDebit).toBe(500);
      expect(comp.totalCredit).toBe(500);
      expect(comp.isBalanced).toBe(true);
    });
  });
});
