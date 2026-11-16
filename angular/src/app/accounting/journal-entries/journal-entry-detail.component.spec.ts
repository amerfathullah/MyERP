import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { describe, it, expect, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { JournalEntryDetailComponent } from './journal-entry-detail.component';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';

describe('JournalEntryDetailComponent Logic', () => {
  // Test GL line total calculations
  function calculateTotals(lines: { isDebit: boolean; amount: number }[]) {
    let totalDebit = 0;
    let totalCredit = 0;
    for (const line of lines) {
      if (line.isDebit) totalDebit += line.amount;
      else totalCredit += line.amount;
    }
    return {
      totalDebit,
      totalCredit,
      difference: Math.abs(totalDebit - totalCredit),
      isBalanced: Math.abs(totalDebit - totalCredit) < 0.01,
    };
  }

  // Test workflow action visibility
  function getWorkflowActions(status: string) {
    const actions: { name: string; label: string; color: string }[] = [];
    if (status === 'Draft') {
      actions.push({ name: 'post', label: 'Post', color: 'success' });
    }
    if (status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', color: 'danger' });
    }
    return actions;
  }

  describe('GL Line Totals', () => {
    it('balanced entry has zero difference', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 1000 },
        { isDebit: false, amount: 1000 },
      ]);
      expect(result.totalDebit).toBe(1000);
      expect(result.totalCredit).toBe(1000);
      expect(result.difference).toBe(0);
      expect(result.isBalanced).toBe(true);
    });

    it('unbalanced entry shows difference', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 1000 },
        { isDebit: false, amount: 800 },
      ]);
      expect(result.difference).toBe(200);
      expect(result.isBalanced).toBe(false);
    });

    it('multi-line balanced entry', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 5000 },
        { isDebit: false, amount: 3000 },
        { isDebit: false, amount: 2000 },
      ]);
      expect(result.totalDebit).toBe(5000);
      expect(result.totalCredit).toBe(5000);
      expect(result.isBalanced).toBe(true);
    });

    it('empty lines gives zero totals', () => {
      const result = calculateTotals([]);
      expect(result.totalDebit).toBe(0);
      expect(result.totalCredit).toBe(0);
      expect(result.isBalanced).toBe(true);
    });

    it('sub-cent difference is still balanced (tolerance)', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 100.005 },
        { isDebit: false, amount: 100.001 },
      ]);
      expect(result.isBalanced).toBe(true); // diff = 0.004 < 0.01
    });

    it('exact 0.01 difference is unbalanced', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 100.01 },
        { isDebit: false, amount: 100.00 },
      ]);
      expect(result.isBalanced).toBe(false);
    });

    it('multi-debit, single credit', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 400 },
        { isDebit: true, amount: 600 },
        { isDebit: false, amount: 1000 },
      ]);
      expect(result.totalDebit).toBe(1000);
      expect(result.totalCredit).toBe(1000);
      expect(result.isBalanced).toBe(true);
    });

    it('large amounts handle correctly', () => {
      const result = calculateTotals([
        { isDebit: true, amount: 1_000_000.50 },
        { isDebit: false, amount: 1_000_000.50 },
      ]);
      expect(result.isBalanced).toBe(true);
    });
  });

  describe('Workflow Actions', () => {
    it('Draft shows Post action only (JE skips Submit step)', () => {
      const actions = getWorkflowActions('Draft');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('post');
      expect(actions[0].color).toBe('success');
    });

    it('Posted shows Cancel action only', () => {
      const actions = getWorkflowActions('Posted');
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('cancel');
      expect(actions[0].color).toBe('danger');
    });

    it('Cancelled shows no actions', () => {
      expect(getWorkflowActions('Cancelled')).toHaveLength(0);
    });

    it('JE has no Submitted state (direct Draft→Post)', () => {
      expect(getWorkflowActions('Submitted')).toHaveLength(0);
    });
  });

  describe('Difference Color Logic', () => {
    function getDifferenceColor(difference: number): string {
      return difference < 0.01 ? 'text-success' : 'text-danger';
    }

    it('zero difference is green (balanced)', () => {
      expect(getDifferenceColor(0)).toBe('text-success');
    });

    it('small difference below threshold is green', () => {
      expect(getDifferenceColor(0.005)).toBe('text-success');
    });

    it('difference at threshold is red', () => {
      expect(getDifferenceColor(0.01)).toBe('text-danger');
    });

    it('large difference is red', () => {
      expect(getDifferenceColor(500)).toBe('text-danger');
    });
  });

  describe('Account Display Format', () => {
    function formatAccount(code?: string, name?: string): string {
      if (code && name) return `${code} — ${name}`;
      if (code) return code;
      if (name) return name;
      return '—';
    }

    it('shows code and name when both available', () => {
      expect(formatAccount('1130', 'Accounts Receivable')).toBe('1130 — Accounts Receivable');
    });

    it('shows code only when name missing', () => {
      expect(formatAccount('1130')).toBe('1130');
    });

    it('shows dash when both missing', () => {
      expect(formatAccount()).toBe('—');
    });
  });

  describe('JournalEntryDetailComponent reversal actions', () => {
    function createComponent() {
      const jeMock = {
        get: vi.fn().mockReturnValue(of({ id: 'je-1', status: 'Posted', lines: [] })),
        post: vi.fn().mockReturnValue(of({})),
        cancel: vi.fn().mockReturnValue(of({})),
        amend: vi.fn().mockReturnValue(of({ id: 'je-am-1' })),
        createReversal: vi.fn().mockReturnValue(of({ id: 'je-rev-1' })),
        createReverseJournalEntry: vi.fn().mockReturnValue(of({ id: 'je-draft-rev-1' })),
      };
      const routerMock = { navigate: vi.fn() };
      const toasterMock = { success: vi.fn(), error: vi.fn() };

      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'je-1' } } } },
          { provide: Router, useValue: routerMock },
          { provide: JournalEntryService, useValue: jeMock },
          { provide: ConfirmationService, useValue: { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) } },
          { provide: ToasterService, useValue: toasterMock },
          { provide: LocalizationService, useValue: { instant: (k: string) => k } },
          { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        ],
      });
      const comp = runInInjectionContext(injector, () => new JournalEntryDetailComponent());
      return { comp, jeMock, routerMock, toasterMock };
    }

    it('hides reversal actions when entry is already a reversal or has an active reversal', () => {
      const { comp } = createComponent();

      comp.je.set({ id: 'je-1', status: 'Posted', voucherType: 0, lines: [] });
      expect(comp.workflowActions.some(a => a.name === 'reverse')).toBe(true);
      expect(comp.workflowActions.some(a => a.name === 'reverse_draft')).toBe(true);

      comp.je.set({ id: 'je-2', status: 'Posted', voucherType: 16, reversalOfId: 'je-1', lines: [] });
      expect(comp.workflowActions.some(a => a.name === 'reverse')).toBe(false);
      expect(comp.workflowActions.some(a => a.name === 'reverse_draft')).toBe(false);

      comp.je.set({ id: 'je-1', status: 'Posted', voucherType: 0, hasActiveReversal: true, lines: [] });
      expect(comp.workflowActions.some(a => a.name === 'reverse')).toBe(false);
    });

    it('wires reverse_draft to createReverseJournalEntry and surfaces errors on failure', () => {
      const { comp, jeMock, routerMock, toasterMock } = createComponent();
      comp.je.set({ id: 'je-1', status: 'Posted', voucherType: 0, lines: [] });

      comp.onAction('reverse_draft');
      expect(jeMock.createReverseJournalEntry).toHaveBeenCalledWith('je-1');
      expect(routerMock.navigate).toHaveBeenCalledWith(['/accounting/journal-entries', 'je-draft-rev-1', 'edit']);

      jeMock.createReversal.mockReturnValue(throwError(() => ({ error: { error: { data: { detail: 'Already reversed' } } } })));
      comp.onAction('reverse');
      expect(toasterMock.error).toHaveBeenCalledWith('Already reversed');
    });
  });
});
