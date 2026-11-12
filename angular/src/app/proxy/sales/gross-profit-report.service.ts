import type { GrossProfitReportDto, GrossProfitRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class GrossProfitReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: GrossProfitRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GrossProfitReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/gross-profit-report/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, groupBy: input.groupBy, customerId: input.customerId, itemId: input.itemId, projectId: input.projectId, invoiceId: input.invoiceId, salesPersonId: input.salesPersonId },
    },
      { apiName: this.apiName,...config });
}
