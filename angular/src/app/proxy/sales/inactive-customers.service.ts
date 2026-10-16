import type {
  InactiveCustomersFilterDto,
  InactiveCustomersReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InactiveCustomersService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: InactiveCustomersFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InactiveCustomersReportDto>(
      {
        method: 'GET',
        url: '/api/app/inactive-customers/report',
        params: {
          companyId: input.companyId,
          docType: input.docType,
          daysSinceLastOrder: input.daysSinceLastOrder,
          customerGroupId: input.customerGroupId,
          territoryId: input.territoryId,
          customerId: input.customerId,
          asOfDate: input.asOfDate,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
