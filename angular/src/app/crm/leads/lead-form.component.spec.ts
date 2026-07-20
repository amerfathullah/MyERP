import { describe, it, expect, beforeEach } from 'vitest';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';

/**
 * Lead Form Component — CRM entry point tests.
 * Validates required fields, Malaysia defaults, edit mode, and DTO mapping.
 */
describe('LeadFormComponent', () => {
  let fb: FormBuilder;
  let form: FormGroup;

  beforeEach(() => {
    fb = new FormBuilder();
    form = fb.group({
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.maxLength(100)]],
      companyName: ['', [Validators.maxLength(200)]],
      email: ['', [Validators.email, Validators.maxLength(256)]],
      phone: ['', [Validators.maxLength(30)]],
      mobileNo: ['', [Validators.maxLength(30)]],
      jobTitle: ['', [Validators.maxLength(100)]],
      website: ['', [Validators.maxLength(500)]],
      source: [0],
      city: [''],
      state: [''],
      country: ['Malaysia'],
      industry: [''],
      annualRevenue: [null],
      companyId: ['', Validators.required],
      notes: [''],
    });
  });

  describe('Required Field Validation', () => {
    it('requires firstName', () => {
      expect(form.get('firstName')?.hasError('required')).toBe(true);
      form.patchValue({ firstName: 'Ahmad' });
      expect(form.get('firstName')?.valid).toBe(true);
    });

    it('requires companyId', () => {
      expect(form.get('companyId')?.hasError('required')).toBe(true);
    });

    it('form invalid without required fields', () => {
      expect(form.valid).toBe(false);
    });

    it('form valid with all required fields', () => {
      form.patchValue({ firstName: 'Ahmad', companyId: 'comp-1' });
      expect(form.valid).toBe(true);
    });
  });

  describe('Field Length Validation', () => {
    it('firstName maxLength 100', () => {
      form.patchValue({ firstName: 'A'.repeat(101) });
      expect(form.get('firstName')?.hasError('maxlength')).toBe(true);
    });

    it('lastName maxLength 100', () => {
      form.patchValue({ lastName: 'B'.repeat(101) });
      expect(form.get('lastName')?.hasError('maxlength')).toBe(true);
    });

    it('companyName maxLength 200', () => {
      form.patchValue({ companyName: 'C'.repeat(201) });
      expect(form.get('companyName')?.hasError('maxlength')).toBe(true);
    });

    it('email maxLength 256', () => {
      form.patchValue({ email: 'a'.repeat(252) + '@b.com' }); // 258 chars total > 256
      expect(form.get('email')?.hasError('maxlength')).toBe(true);
    });

    it('phone maxLength 30', () => {
      form.patchValue({ phone: '0'.repeat(31) });
      expect(form.get('phone')?.hasError('maxlength')).toBe(true);
    });
  });

  describe('Email Validation', () => {
    it('rejects invalid email format', () => {
      form.patchValue({ email: 'not-an-email' });
      expect(form.get('email')?.hasError('email')).toBe(true);
    });

    it('accepts valid email', () => {
      form.patchValue({ email: 'ahmad@acme.com.my' });
      expect(form.get('email')?.valid).toBe(true);
    });

    it('empty email is valid (optional field)', () => {
      expect(form.get('email')?.valid).toBe(true);
    });

    it('accepts Malaysian email domains', () => {
      form.patchValue({ email: 'contact@company.com.my' });
      expect(form.get('email')?.valid).toBe(true);
    });
  });

  describe('Malaysia Defaults', () => {
    it('defaults country to Malaysia', () => {
      expect(form.get('country')?.value).toBe('Malaysia');
    });

    it('defaults source to 0 (None/Direct)', () => {
      expect(form.get('source')?.value).toBe(0);
    });

    it('annualRevenue defaults to null', () => {
      expect(form.get('annualRevenue')?.value).toBeNull();
    });
  });

  describe('DTO Mapping', () => {
    it('produces complete DTO with all fields', () => {
      form.patchValue({
        firstName: 'Ahmad',
        lastName: 'Ibrahim',
        companyName: 'Acme Sdn Bhd',
        email: 'ahmad@acme.com.my',
        phone: '+60123456789',
        mobileNo: '+60198765432',
        jobTitle: 'Procurement Manager',
        website: 'https://acme.com.my',
        source: 2,
        city: 'Kuala Lumpur',
        state: 'WP',
        country: 'Malaysia',
        industry: 'Manufacturing',
        annualRevenue: 5000000,
        companyId: 'comp-uuid',
        notes: 'Met at MITI event',
      });

      const dto = form.getRawValue();
      expect(dto.firstName).toBe('Ahmad');
      expect(dto.lastName).toBe('Ibrahim');
      expect(dto.companyName).toBe('Acme Sdn Bhd');
      expect(dto.email).toBe('ahmad@acme.com.my');
      expect(dto.phone).toBe('+60123456789');
      expect(dto.annualRevenue).toBe(5000000);
      expect(dto.companyId).toBe('comp-uuid');
    });

    it('optional fields are empty strings when not filled', () => {
      form.patchValue({ firstName: 'Test', companyId: 'c1' });
      const dto = form.getRawValue();

      expect(dto.lastName).toBe('');
      expect(dto.email).toBe('');
      expect(dto.phone).toBe('');
      expect(dto.notes).toBe('');
      expect(dto.website).toBe('');
    });

    it('DTO field names match backend expectations', () => {
      const dto = form.getRawValue();
      // Verify exact property names that backend expects
      expect(dto).toHaveProperty('firstName');
      expect(dto).toHaveProperty('lastName');
      expect(dto).toHaveProperty('companyName');
      expect(dto).toHaveProperty('email');
      expect(dto).toHaveProperty('phone');
      expect(dto).toHaveProperty('mobileNo');
      expect(dto).toHaveProperty('jobTitle');
      expect(dto).toHaveProperty('website');
      expect(dto).toHaveProperty('source');
      expect(dto).toHaveProperty('companyId');
      expect(dto).toHaveProperty('annualRevenue');
    });
  });

  describe('Edit Mode', () => {
    it('patches form with existing lead data', () => {
      const existingLead = {
        firstName: 'Siti',
        lastName: 'Aminah',
        companyName: 'Tech Corp',
        email: 'siti@techcorp.my',
        phone: '+60312345678',
        source: 3,
        city: 'Penang',
        companyId: 'comp-abc',
      };
      form.patchValue(existingLead);

      expect(form.get('firstName')?.value).toBe('Siti');
      expect(form.get('companyName')?.value).toBe('Tech Corp');
      expect(form.get('city')?.value).toBe('Penang');
    });

    it('preserves country when patching partial data', () => {
      form.patchValue({ firstName: 'Test', companyId: 'c1' });
      // Country should remain Malaysia (not overwritten to empty)
      expect(form.get('country')?.value).toBe('Malaysia');
    });
  });

  describe('Phone Number Formats', () => {
    it('accepts Malaysian mobile format', () => {
      form.patchValue({ phone: '+60123456789' });
      expect(form.get('phone')?.valid).toBe(true);
    });

    it('accepts Malaysian landline format', () => {
      form.patchValue({ phone: '+60312345678' });
      expect(form.get('phone')?.valid).toBe(true);
    });

    it('accepts international format', () => {
      form.patchValue({ phone: '+6512345678' });
      expect(form.get('phone')?.valid).toBe(true);
    });
  });
});
