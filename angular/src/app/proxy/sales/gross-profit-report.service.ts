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
      url: '/api/app/gross-profit-report/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}