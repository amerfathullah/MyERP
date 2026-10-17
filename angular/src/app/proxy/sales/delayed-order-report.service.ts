import type { DelayedOrderFilterDto, DelayedOrderReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DelayedOrderReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: DelayedOrderFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DelayedOrderReportDto>({
      method: 'GET',
      url: '/api/app/delayed-order-report/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        customerId: input.customerId,
        consolidated: input.consolidated,
        delayedOnly: input.delayedOnly,
        searchText: input.searchText,
      },
    },
    { apiName: this.apiName, ...config });
}
