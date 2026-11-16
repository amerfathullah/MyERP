import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { ProformaInvoiceDetailComponent } from './proforma-invoice-detail.component';
import { ProformaInvoiceService } from '../../proxy/sales/proforma-invoice.service';

describe('ProformaInvoiceDetailComponent', () => {
  let serviceMock: any;
  let toasterMock: any;
  let confirmationMock: any;

  function createComponent(): ProformaInvoiceDetailComponent {
    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'pi-1' } } } },
        { provide: ProformaInvoiceService, useValue: serviceMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: ConfirmationService, useValue: confirmationMock },
      ],
    });
    return runInInjectionContext(injector, () => new ProformaInvoiceDetailComponent());
  }

  beforeEach(() => {
    serviceMock = {
      get: vi.fn().mockReturnValue(of({ id: 'pi-1', proformaNumber: 'PI-001', status: 1 })),
      getEmailContent: vi.fn().mockReturnValue(of({ subject: 'Proforma PI-001', message: 'Please review attached proforma.' })),
      sendEmail: vi.fn().mockReturnValue(of(void 0)),
      cancel: vi.fn().mockReturnValue(of(void 0)),
    };
    toasterMock = { success: vi.fn(), error: vi.fn() };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
  });

  it('preloads email subject and body via getEmailContent when opening email panel', () => {
    const comp = createComponent();
    comp.ngOnInit();

    comp.openEmailPanel();

    expect(comp.showEmailPanel).toBe(true);
    expect(serviceMock.getEmailContent).toHaveBeenCalledWith('pi-1');
    expect(comp.emailSubject).toBe('Proforma PI-001');
    expect(comp.emailMessage).toBe('Please review attached proforma.');
    expect(comp.loadingEmailContent).toBe(false);
  });

  it('submits custom subject and message with sendEmail and resets form state', () => {
    const comp = createComponent();
    comp.ngOnInit();
    comp.openEmailPanel();
    comp.emailRecipients = 'buyer@example.com';

    comp.sendEmail();

    expect(serviceMock.sendEmail).toHaveBeenCalledWith('pi-1', {
      recipients: 'buyer@example.com',
      subject: 'Proforma PI-001',
      message: 'Please review attached proforma.',
    });
    expect(comp.showEmailPanel).toBe(false);
    expect(comp.emailRecipients).toBe('');
    expect(comp.emailSubject).toBe('');
    expect(toasterMock.success).toHaveBeenCalledWith('::EmailSent');
  });

  it('resets loadingEmailContent when getEmailContent fails', () => {
    serviceMock.getEmailContent.mockReturnValue(throwError(() => new Error('network')));
    const comp = createComponent();
    comp.ngOnInit();

    comp.openEmailPanel();

    expect(comp.loadingEmailContent).toBe(false);
  });
});
