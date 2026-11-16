import { Injector, runInInjectionContext } from '@angular/core';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { GlRepostComponent } from './gl-repost.component';
import { GlRepostService } from '../../proxy/accounting/gl-repost.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

describe('GlRepostComponent Allowance Check', () => {
  function createComponent() {
    const repostServiceMock = {
      getAllowedVoucherTypes: vi.fn().mockReturnValue(of(['SalesInvoice', 'JournalEntry'])),
      isRepostAllowed: vi.fn().mockImplementation((type: string) => of(type !== 'JournalEntry')),
      repost: vi.fn(),
    };

    const injector = Injector.create({
      providers: [
        { provide: GlRepostService, useValue: repostServiceMock },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ToasterService, useValue: { success: vi.fn(), warn: vi.fn(), error: vi.fn() } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new GlRepostComponent());
    return { comp, repostServiceMock };
  }

  it('checks isRepostAllowed for initial and changed voucher types', () => {
    const { comp, repostServiceMock } = createComponent();
    expect(repostServiceMock.isRepostAllowed).toHaveBeenCalledWith('SalesInvoice');
    expect(comp.isVoucherTypeAllowed()).toBe(true);

    comp.checkRepostAllowed('JournalEntry');
    expect(repostServiceMock.isRepostAllowed).toHaveBeenCalledWith('JournalEntry');
    expect(comp.isVoucherTypeAllowed()).toBe(false);
  });
});
