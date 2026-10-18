import type {
  FixUncancelledEntriesInputDto,
  FixUncancelledEntriesResultDto,
  StockLedgerEntriesOfCancelledVouchersFilterDto,
  StockLedgerEntriesOfCancelledVouchersReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StockLedgerEntriesOfCancelledVouchersService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: StockLedgerEntriesOfCancelledVouchersFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StockLedgerEntriesOfCancelledVouchersReportDto>(
      {
        method: 'GET',
        url: '/api/app/stock-ledger-entries-of-cancelled-vouchers/report',
        params: {
          companyId: input.companyId,
          voucherType: input.voucherType,
          itemId: input.itemId,
          warehouseId: input.warehouseId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );

  fixUncancelledEntries = (input: FixUncancelledEntriesInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, FixUncancelledEntriesResultDto>(
      {
        method: 'POST',
        url: '/api/app/stock-ledger-entries-of-cancelled-vouchers/fix-uncancelled-entries',
        body: input,
      },
      { apiName: this.apiName, ...config }
    );
}
