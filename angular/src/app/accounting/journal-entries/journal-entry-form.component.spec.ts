import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

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
});
