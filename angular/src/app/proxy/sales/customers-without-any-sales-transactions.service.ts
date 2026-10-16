import type {
  CustomersWithoutAnySalesTransactionsFilterDto,
  CustomersWithoutAnySalesTransactionsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CustomersWithoutAnySalesTransactionsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: CustomersWithoutAnySalesTransactionsFilterDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, CustomersWithoutAnySalesTransactionsReportDto>(
      {
        method: 'GET',
        url: '/api/app/customers-without-any-sales-transactions/report',
        params: {
          companyId: input.companyId,
          customerGroupId: input.customerGroupId,
          territoryId: input.territoryId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
