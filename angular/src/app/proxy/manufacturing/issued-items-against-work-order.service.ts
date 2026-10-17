import type { IssuedItemsAgainstWorkOrderReportDto, IssuedItemsAgainstWorkOrderRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class IssuedItemsAgainstWorkOrderService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: IssuedItemsAgainstWorkOrderRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IssuedItemsAgainstWorkOrderReportDto>({
      method: 'POST',
      url: '/api/app/issued-items-against-work-order/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
