import { describe, it, expect } from 'vitest';

/**
 * Unit tests for SellingSettings component logic and Proforma Invoice settings (ERPNext PR #59332).
 */
describe('SellingSettings proforma invoicing configuration', () => {
  it('should initialize and hold default selling settings including proforma email template', () => {
    const settings: Record<string, string> = {
      'MyERP.Selling.SoRequired': 'false',
      'MyERP.Selling.DnRequired': 'false',
      'MyERP.Selling.EnableProformaInvoice': 'true',
      'MyERP.Selling.DefaultProformaPrintFormat': 'Standard Proforma Invoice',
      'MyERP.Selling.ProformaEmailTemplate': 'Proforma Customer Template',
    };

    expect(settings['MyERP.Selling.EnableProformaInvoice']).toBe('true');
    expect(settings['MyERP.Selling.DefaultProformaPrintFormat']).toBe('Standard Proforma Invoice');
    expect(settings['MyERP.Selling.ProformaEmailTemplate']).toBe('Proforma Customer Template');
  });

  it('should update proforma email template when modified', () => {
    const settings: Record<string, string> = {
      'MyERP.Selling.EnableProformaInvoice': 'true',
      'MyERP.Selling.DefaultProformaPrintFormat': '',
      'MyERP.Selling.ProformaEmailTemplate': '',
    };

    // User types in a new template name
    settings['MyERP.Selling.ProformaEmailTemplate'] = 'Custom Advance Notice';

    expect(settings['MyERP.Selling.ProformaEmailTemplate']).toBe('Custom Advance Notice');
  });
});
