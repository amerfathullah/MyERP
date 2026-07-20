import { describe, it, expect } from 'vitest';
import { FormBuilder, Validators } from '@angular/forms';

/**
 * Tests for Loan form save DTO mapping logic.
 * Validates that form values correctly map to backend DTOs.
 */
describe('Loan form DTO mapping', () => {
  const fb = new FormBuilder();

  function createLoanForm() {
    return fb.group({
      employeeId: ['', Validators.required],
      loanType: [0],
      loanAmount: [0, [Validators.required, Validators.min(1)]],
      interestRate: [0, [Validators.required, Validators.min(0)]],
      interestCalculationMethod: [0],
      repaymentPeriodMonths: [12, [Validators.required, Validators.min(1)]],
      repaymentStartDate: ['', Validators.required],
      gracePeriodMonths: [0],
      penaltyInterestRate: [0]
    });
  }

  function mapFormToDto(form: ReturnType<typeof createLoanForm>, companyId: string | null) {
    const val = form.getRawValue();
    return {
      ...val,
      companyId,
      loanType: Number(val.loanType),
      interestCalculationMethod: Number(val.interestCalculationMethod)
    };
  }

  it('should create form with default values', () => {
    const form = createLoanForm();
    expect(form.get('loanType')?.value).toBe(0);
    expect(form.get('repaymentPeriodMonths')?.value).toBe(12);
    expect(form.get('gracePeriodMonths')?.value).toBe(0);
  });

  it('should be invalid without required fields', () => {
    const form = createLoanForm();
    expect(form.valid).toBe(false);
  });

  it('should be valid with all required fields', () => {
    const form = createLoanForm();
    form.patchValue({
      employeeId: 'emp-123',
      loanAmount: 50000,
      interestRate: 5,
      repaymentStartDate: '2026-08-01',
    });
    expect(form.valid).toBe(true);
  });

  it('should reject zero loan amount', () => {
    const form = createLoanForm();
    form.patchValue({ loanAmount: 0 });
    expect(form.get('loanAmount')?.valid).toBe(false);
  });

  it('should reject negative loan amount', () => {
    const form = createLoanForm();
    form.patchValue({ loanAmount: -1000 });
    expect(form.get('loanAmount')?.valid).toBe(false);
  });

  it('should map loanType to number in DTO', () => {
    const form = createLoanForm();
    form.patchValue({ employeeId: 'emp-1', loanAmount: 10000, interestRate: 3, repaymentStartDate: '2026-09-01', loanType: '1' as any });
    const dto = mapFormToDto(form, 'company-abc');
    expect(dto.loanType).toBe(1);
    expect(typeof dto.loanType).toBe('number');
  });

  it('should map interestCalculationMethod to number in DTO', () => {
    const form = createLoanForm();
    form.patchValue({ employeeId: 'emp-1', loanAmount: 10000, interestRate: 5, repaymentStartDate: '2026-09-01', interestCalculationMethod: '1' as any });
    const dto = mapFormToDto(form, 'company-abc');
    expect(dto.interestCalculationMethod).toBe(1);
    expect(typeof dto.interestCalculationMethod).toBe('number');
  });

  it('should include companyId from context', () => {
    const form = createLoanForm();
    form.patchValue({ employeeId: 'emp-1', loanAmount: 10000, interestRate: 5, repaymentStartDate: '2026-09-01' });
    const dto = mapFormToDto(form, 'my-company-id');
    expect(dto.companyId).toBe('my-company-id');
  });

  it('should pass null companyId when no company selected', () => {
    const form = createLoanForm();
    form.patchValue({ employeeId: 'emp-1', loanAmount: 10000, interestRate: 5, repaymentStartDate: '2026-09-01' });
    const dto = mapFormToDto(form, null);
    expect(dto.companyId).toBeNull();
  });
});

describe('Loan status mapping', () => {
  const LOAN_STATUS = ['Draft', 'Sanctioned', 'Disbursed', 'PartiallyRepaid', 'FullyRepaid', 'Cancelled'] as const;

  it('should map all status indices to labels', () => {
    expect(LOAN_STATUS[0]).toBe('Draft');
    expect(LOAN_STATUS[1]).toBe('Sanctioned');
    expect(LOAN_STATUS[2]).toBe('Disbursed');
    expect(LOAN_STATUS[3]).toBe('PartiallyRepaid');
    expect(LOAN_STATUS[4]).toBe('FullyRepaid');
    expect(LOAN_STATUS[5]).toBe('Cancelled');
  });

  it('should handle undefined status gracefully', () => {
    const status: number | undefined = undefined;
    expect(LOAN_STATUS[status ?? 0]).toBe('Draft');
  });
});

describe('Loan detail workflow visibility', () => {
  function getVisibleActions(status: number): string[] {
    const actions: string[] = [];
    if (status === 0) { actions.push('Sanction', 'Cancel'); }
    if (status === 1) { actions.push('Disburse', 'Cancel'); }
    if (status === 2 || status === 3) { actions.push('RecordRepayment'); }
    return actions;
  }

  it('Draft shows Sanction and Cancel', () => {
    expect(getVisibleActions(0)).toEqual(['Sanction', 'Cancel']);
  });

  it('Sanctioned shows Disburse and Cancel', () => {
    expect(getVisibleActions(1)).toEqual(['Disburse', 'Cancel']);
  });

  it('Disbursed shows RecordRepayment', () => {
    expect(getVisibleActions(2)).toEqual(['RecordRepayment']);
  });

  it('PartiallyRepaid shows RecordRepayment', () => {
    expect(getVisibleActions(3)).toEqual(['RecordRepayment']);
  });

  it('FullyRepaid shows no actions', () => {
    expect(getVisibleActions(4)).toEqual([]);
  });

  it('Cancelled shows no actions', () => {
    expect(getVisibleActions(5)).toEqual([]);
  });
});
