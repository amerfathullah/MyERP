import { Injector, runInInjectionContext } from '@angular/core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CodeListListComponent } from './code-list-list.component';
import { CodeListService } from '../../proxy/edi/code-list.service';

describe('CodeListListComponent Default Code Resolution', () => {
  it('resolves default code via CodeListService.getDefaultCode using canonicalUri or title', () => {
    const serviceMock = {
      getList: vi.fn().mockReturnValue(of({
        items: [{ id: 'cl-1', title: 'LHDN Tax Types', canonicalUri: 'urn:myinvois:tax-type', isActive: true }],
      })),
      getDefaultCode: vi.fn().mockReturnValue(of('01')),
    };

    const injector = Injector.create({
      providers: [
        { provide: CodeListService, useValue: serviceMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CodeListListComponent());
    comp.ngOnInit();
    comp.resolveDefaultCode(comp.items[0]);

    expect(serviceMock.getDefaultCode).toHaveBeenCalledWith('urn:myinvois:tax-type');
    expect(comp.defaultCodes['cl-1']).toBe('01');
  });
});
