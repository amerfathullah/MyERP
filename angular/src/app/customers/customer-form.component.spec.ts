import { describe, it, expect } from 'vitest';
import { FormBuilder, Validators } from '@angular/forms';

/**
 * Tests for Customer form validation and DTO mapping.
 * Covers: required fields, Malaysia-specific fields (TIN, SST, BRN), email validation.
 */
describe('Customer form logic', () => {
  const fb = new FormBuilder();

  function createCustomerForm() {
    return fb.group({
      companyId: ['', Validators.required],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      customerCode: [''],
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
      const form = createCustomerForm();
      expect(form.get('companyId')?.valid).toBe(false);
    });

    it('should require name', () => {
      const form = createCustomerForm();
      expect(form.get('name')?.valid).toBe(false);
    });

    it('should reject name over 200 chars', () => {
      const form = createCustomerForm();
      form.patchValue({ name: 'A'.repeat(201) });
      expect(form.get('name')?.valid).toBe(false);
    });

    it('should accept valid name', () => {
      const form = createCustomerForm();
      form.patchValue({ name: 'ABC Trading Sdn Bhd' });
      expect(form.get('name')?.valid).toBe(true);
    });

    it('should validate email format', () => {
      const form = createCustomerForm();
      form.patchValue({ email: 'not-an-email' });
      expect(form.get('email')?.valid).toBe(false);
    });

    it('should accept valid email', () => {
      const form = createCustomerForm();
      form.patchValue({ email: 'contact@company.my' });
      expect(form.get('email')?.valid).toBe(true);
    });

    it('should accept empty email (optional)', () => {
      const form = createCustomerForm();
      expect(form.get('email')?.valid).toBe(true);
    });

    it('should be valid with required fields filled', () => {
      const form = createCustomerForm();
      form.patchValue({ companyId: 'comp-1', name: 'Test Customer' });
      expect(form.valid).toBe(true);
    });
  });

  describe('defaults', () => {
    it('should default country to MYS', () => {
      const form = createCustomerForm();
      expect(form.get('country')?.value).toBe('MYS');
    });

    it('should default idType to BRN', () => {
      const form = createCustomerForm();
      expect(form.get('idType')?.value).toBe('BRN');
    });

    it('should default isActive to true', () => {
      const form = createCustomerForm();
      expect(form.get('isActive')?.value).toBe(true);
    });
  });

  describe('Malaysia compliance fields', () => {
    it('should support TIN field', () => {
      const form = createCustomerForm();
      form.patchValue({ tin: 'C12345678910' });
      expect(form.get('tin')?.value).toBe('C12345678910');
    });

    it('should support SST registration', () => {
      const form = createCustomerForm();
      form.patchValue({ sstRegistrationNumber: 'W10-1234-56789012' });
      expect(form.get('sstRegistrationNumber')?.value).toBe('W10-1234-56789012');
    });

    it('should support business registration number', () => {
      const form = createCustomerForm();
      form.patchValue({ registrationNumber: '201901000123' });
      expect(form.get('registrationNumber')?.value).toBe('201901000123');
    });
  });

  describe('DTO mapping', () => {
    it('should produce complete DTO from form', () => {
      const form = createCustomerForm();
      form.patchValue({
        companyId: 'comp-1',
        name: 'XYZ Corp Sdn Bhd',
        tin: 'C12345678910',
        registrationNumber: '202001000456',
        sstRegistrationNumber: 'W10-1234-56789012',
        email: 'info@xyzcorp.my',
        phone: '+6012-3456789',
        country: 'MYS',
      });

      const dto = form.getRawValue();
      expect(dto.companyId).toBe('comp-1');
      expect(dto.name).toBe('XYZ Corp Sdn Bhd');
      expect(dto.tin).toBe('C12345678910');
      expect(dto.country).toBe('MYS');
      expect(dto.isActive).toBe(true);
    });

    it('should include empty optional fields as empty strings', () => {
      const form = createCustomerForm();
      form.patchValue({ companyId: 'comp-1', name: 'Test' });
      const dto = form.getRawValue();
      expect(dto.customerCode).toBe('');
      expect(dto.address).toBe('');
      expect(dto.city).toBe('');
    });
  });
});
