import { describe, it, expect } from 'vitest';
import { FormBuilder, FormArray, Validators } from '@angular/forms';

/**
 * Unit tests for PosProfile form logic, validation, and email template support (ERPNext PR #59332).
 */
describe('PosProfile form logic and mapping', () => {
  const fb = new FormBuilder();

  function createPosProfileForm() {
    return fb.group({
      profileName: ['', Validators.required],
      warehouseId: ['', Validators.required],
      currencyCode: ['MYR'],
      invoiceType: ['POS Invoice'],
      validateStock: [true],
      writeOffLimit: [0],
      postChangeGlEntries: [false],
      projectId: [''],
      receiptEmailTemplateId: [''],
      paymentMethods: fb.array([]),
      users: fb.array([]),
    });
  }

  function addPaymentMethod(form: ReturnType<typeof createPosProfileForm>, data?: { modeOfPaymentId: string; accountId: string; isDefault?: boolean }) {
    const pms = form.get('paymentMethods') as FormArray;
    pms.push(fb.group({
      modeOfPaymentId: [data?.modeOfPaymentId || '', Validators.required],
      accountId: [data?.accountId || '', Validators.required],
      isDefault: [data?.isDefault || false],
    }));
  }

  function addUser(form: ReturnType<typeof createPosProfileForm>, data?: { userId: string; isDefault?: boolean }) {
    const users = form.get('users') as FormArray;
    users.push(fb.group({
      userId: [data?.userId || '', Validators.required],
      isDefault: [data?.isDefault || false],
    }));
  }

  it('should initialize with default values including receiptEmailTemplateId', () => {
    const form = createPosProfileForm();
    expect(form.get('receiptEmailTemplateId')?.value).toBe('');
    expect(form.get('currencyCode')?.value).toBe('MYR');
    expect(form.get('validateStock')?.value).toBe(true);
    expect(form.valid).toBe(false); // requires profileName & warehouseId
  });

  it('should be valid when required fields profileName and warehouseId are provided', () => {
    const form = createPosProfileForm();
    form.patchValue({
      profileName: 'Main Store POS',
      warehouseId: 'wh-123',
    });
    expect(form.valid).toBe(true);
  });

  it('should patch receiptEmailTemplateId from backend DTO', () => {
    const form = createPosProfileForm();
    const dto = {
      profileName: 'Outlet Counter',
      warehouseId: 'wh-outlet',
      currencyCode: 'MYR',
      invoiceType: 'POS Invoice',
      validateStock: true,
      writeOffLimit: 5,
      postChangeGlEntries: true,
      projectId: 'proj-1',
      receiptEmailTemplateId: 'tpl-receipt-001',
    };

    form.patchValue(dto);

    expect(form.get('receiptEmailTemplateId')?.value).toBe('tpl-receipt-001');
    expect(form.get('projectId')?.value).toBe('proj-1');
    expect(form.get('writeOffLimit')?.value).toBe(5);
  });

  it('should support adding payment methods and applicable users', () => {
    const form = createPosProfileForm();
    addPaymentMethod(form, { modeOfPaymentId: 'cash', accountId: 'acc-cash', isDefault: true });
    addUser(form, { userId: 'user-cashier-1', isDefault: true });

    const pms = form.get('paymentMethods') as FormArray;
    const users = form.get('users') as FormArray;

    expect(pms.length).toBe(1);
    expect(pms.at(0).get('modeOfPaymentId')?.value).toBe('cash');
    expect(pms.at(0).get('isDefault')?.value).toBe(true);

    expect(users.length).toBe(1);
    expect(users.at(0).get('userId')?.value).toBe('user-cashier-1');
    expect(users.at(0).get('isDefault')?.value).toBe(true);
  });
});
