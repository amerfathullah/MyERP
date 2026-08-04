import type { SalesCommissionReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesCommissionReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalesCommissionReportDto>({
      method: 'GET',
      url: `/api/app/sales-commission-report/report/${companyId}`,
      params: { fromDate, toDate },
    },
    { apiName: this.apiName,...config });
}