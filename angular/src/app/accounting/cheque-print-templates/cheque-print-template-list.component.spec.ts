import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ChequePrintTemplateListComponent } from './cheque-print-template-list.component';
import { ChequePrintTemplateService } from '../../proxy/accounting/cheque-print-template.service';

describe('ChequePrintTemplateListComponent', () => {
  let serviceMock: any;
  let confirmationMock: any;
  let toasterMock: any;
  let sanitizerMock: any;

  function createComponent(): ChequePrintTemplateListComponent {
    const injector = Injector.create({
      providers: [
        { provide: ChequePrintTemplateService, useValue: serviceMock },
        { provide: ConfirmationService, useValue: confirmationMock },
        { provide: ToasterService, useValue: toasterMock },
        { provide: DomSanitizer, useValue: sanitizerMock },
      ],
    });
    return runInInjectionContext(injector, () => new ChequePrintTemplateListComponent());
  }

  beforeEach(() => {
    serviceMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'c1', bankName: 'Maybank' }] })),
      delete: vi.fn().mockReturnValue(of(void 0)),
      generatePreview: vi.fn().mockReturnValue(of({ htmlContent: '<div>Maybank Preview</div>' })),
    };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
    toasterMock = { success: vi.fn(), error: vi.fn() };
    sanitizerMock = { bypassSecurityTrustHtml: vi.fn((html: string) => `SAFE:${html}`) };
  });

  it('loads templates on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(serviceMock.getList).toHaveBeenCalledTimes(1);
    expect(comp.items).toHaveLength(1);
  });

  it('fetches preview HTML via generatePreview and sanitizes it for sandboxed iframe', () => {
    const comp = createComponent();
    const tpl = { id: 'c1', bankName: 'Maybank' } as any;

    comp.preview(tpl);

    expect(comp.previewTemplate()).toEqual(tpl);
    expect(serviceMock.generatePreview).toHaveBeenCalledWith('c1');
    expect(comp.previewRawHtml()).toBe('<div>Maybank Preview</div>');
    expect(sanitizerMock.bypassSecurityTrustHtml).toHaveBeenCalledWith('<div>Maybank Preview</div>');
    expect(comp.previewHtml()).toBe('SAFE:<div>Maybank Preview</div>');
    expect(comp.isLoadingPreview()).toBe(false);
  });

  it('toggles preview closed when clicking the active template again', () => {
    const comp = createComponent();
    const tpl = { id: 'c1', bankName: 'Maybank' } as any;

    comp.preview(tpl);
    expect(comp.previewTemplate()).toEqual(tpl);

    comp.preview(tpl);
    expect(comp.previewTemplate()).toBeNull();
    expect(comp.previewRawHtml()).toBe('');
    expect(comp.previewHtml()).toBeNull();
  });

  it('handles preview API failure', () => {
    serviceMock.generatePreview.mockReturnValue(throwError(() => ({ error: { error: { message: 'Missing template' } } })));
    const comp = createComponent();

    comp.preview({ id: 'c2', bankName: 'CIMB' } as any);

    expect(comp.isLoadingPreview()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Missing template');
  });
});
