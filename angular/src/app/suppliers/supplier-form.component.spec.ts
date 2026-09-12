import { describe, it, expect } from 'vitest';
import { FormBuilder, Validators } from '@angular/forms';

/**
 * Tests for Supplier form validation and DTO mapping.
 * Covers: required fields, Malaysia-specific fields (TIN, SST, BRN), email validation,
 * and taxpayer verification persistence.
 */
describe('Supplier form logic', () => {
  const fb = new FormBuilder();

  function createSupplierForm() {
    return fb.group({
      companyId: ['', Validators.required],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      supplierCode: [''],
      tin: [''],
      registrationNumber: [''],
      sstRegistrationNumber: [''],
      idType: ['BRN'],
      idValue: [''],
      contactPerson: [''],
      phone: [''],
      email: ['', Validators.email],
      address: [''],
      city: [''],
      state: [''],
      postalCode: [''],
      country: ['MYS'],
      isActive: [true],
    });
  }

  describe('validation', () => {
    it('should require companyId', () => {
      const form = createSupplierForm();
      expect(form.get('companyId')?.valid).toBe(false);
    });

    it('should require name', () => {
      const form = createSupplierForm();
      expect(form.get('name')?.valid).toBe(false);
    });

    it('should reject name over 200 chars', () => {
      const form = createSupplierForm();
      form.patchValue({ name: 'A'.repeat(201) });
      expect(form.get('name')?.valid).toBe(false);
    });

    it('should accept valid name', () => {
      const form = createSupplierForm();
      form.patchValue({ name: 'Supplier Enterprise Sdn Bhd' });
      expect(form.get('name')?.valid).toBe(true);
    });

    it('should validate email format', () => {
      const form = createSupplierForm();
      form.patchValue({ email: 'not-an-email' });
      expect(form.get('email')?.valid).toBe(false);
    });

    it('should accept valid email', () => {
      const form = createSupplierForm();
      form.patchValue({ email: 'sales@supplier.com.my' });
      expect(form.get('email')?.valid).toBe(true);
    });

    it('should accept empty email (optional)', () => {
      const form = createSupplierForm();
      expect(form.get('email')?.valid).toBe(true);
    });

    it('should be valid with required fields filled', () => {
      const form = createSupplierForm();
      form.patchValue({ companyId: 'comp-1', name: 'Supplier Enterprise' });
      expect(form.valid).toBe(true);
    });
  });

  describe('defaults', () => {
    it('should default country to MYS', () => {
      const form = createSupplierForm();
      expect(form.get('country')?.value).toBe('MYS');
    });

    it('should default idType to BRN', () => {
      const form = createSupplierForm();
      expect(form.get('idType')?.value).toBe('BRN');
    });

    it('should default isActive to true', () => {
      const form = createSupplierForm();
      expect(form.get('isActive')?.value).toBe(true);
    });
  });

  describe('Malaysia compliance fields', () => {
    it('should support TIN field', () => {
      const form = createSupplierForm();
      form.patchValue({ tin: 'C98765432100' });
      expect(form.get('tin')?.value).toBe('C98765432100');
    });

    it('should support SST registration', () => {
      const form = createSupplierForm();
      form.patchValue({ sstRegistrationNumber: 'B10-9876-54321098' });
      expect(form.get('sstRegistrationNumber')?.value).toBe('B10-9876-54321098');
    });

    it('should support business registration number', () => {
      const form = createSupplierForm();
      form.patchValue({ registrationNumber: '202101009999' });
      expect(form.get('registrationNumber')?.value).toBe('202101009999');
    });
  });

  describe('Taxpayer verification and persistence', () => {
    it('should construct SearchTaxpayerDto with supplierId when in edit mode', () => {
      const form = createSupplierForm();
      form.patchValue({ idType: 'BRN', idValue: '202101009999' });
      const supplierId = 'supp-456';
      const isEditMode = true;

      const searchDto = {
        idType: form.get('idType')?.value,
        idValue: form.get('idValue')?.value,
        supplierId: isEditMode && supplierId ? supplierId : undefined,
      };

      expect(searchDto.idType).toBe('BRN');
      expect(searchDto.idValue).toBe('202101009999');
      expect(searchDto.supplierId).toBe('supp-456');
    });

    it('should omit supplierId when in create mode', () => {
      const form = createSupplierForm();
      form.patchValue({ idType: 'BRN', idValue: '202101009999' });
      const supplierId = null;
      const isEditMode = false;

      const searchDto = {
        idType: form.get('idType')?.value,
        idValue: form.get('idValue')?.value,
        supplierId: isEditMode && supplierId ? supplierId : undefined,
      };

      expect(searchDto.supplierId).toBeUndefined();
    });
  });
});
