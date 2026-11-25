import { Injector, runInInjectionContext } from '@angular/core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { CallLogListComponent } from './call-log-list.component';
import { CallLogService } from '../../proxy/telephony/call-log.service';
import { CallStatus } from '../../proxy/telephony/call-status.enum';

describe('CallLogListComponent Fail Call', () => {
  it('marks ringing/in-progress call as failed via CallLogService.failCall and reloads', () => {
    const callMock = {
      getList: vi.fn().mockReturnValue(of({ items: [{ id: 'call-1', callId: 'C-01', status: CallStatus.Ringing }] })),
      failCall: vi.fn().mockReturnValue(of({ id: 'call-1', status: CallStatus.Failed })),
    };
    const toasterMock = { success: vi.fn(), warn: vi.fn(), error: vi.fn() };
    const injector = Injector.create({
      providers: [
        { provide: CallLogService, useValue: callMock },
        { provide: ConfirmationService, useValue: { warn: vi.fn() } },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new CallLogListComponent());
    comp.ngOnInit();
    comp.failCall(comp.items[0]);

    expect(callMock.failCall).toHaveBeenCalledWith('call-1', CallStatus.Failed);
    expect(toasterMock.warn).toHaveBeenCalledWith('::CallMarkedFailed');
    expect(callMock.getList).toHaveBeenCalledTimes(2);
  });
});
