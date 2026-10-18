import type { SerialNoLedgerReportDto, SerialNoLedgerFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialNoLedgerService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SerialNoLedgerFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialNoLedgerReportDto>({
      method: 'GET',
      url: '/api/app/serial-no-ledger/report',
      params: {
        companyId: input.companyId,
        warehouseId: input.warehouseId,
        itemId: input.itemId,
        serialNumber: input.serialNumber,
        fromDate: input.fromDate,
        toDate: input.toDate,
        voucherType: input.voucherType,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName, ...config });
}
