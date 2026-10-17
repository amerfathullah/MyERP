import type { DelayedItemReportDto, DelayedItemFilterDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DelayedItemReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: DelayedItemFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DelayedItemReportDto>({
      method: 'GET',
      url: '/api/app/delayed-item-report/report',
      params: {
        companyId: input.companyId,
        basedOn: input.basedOn,
        fromDate: input.fromDate,
        toDate: input.toDate,
        customerId: input.customerId,
        salesOrderId: input.salesOrderId,
        itemGroupId: input.itemGroupId,
        delayedOnly: input.delayedOnly,
      },
    },
    { apiName: this.apiName, ...config });
}
