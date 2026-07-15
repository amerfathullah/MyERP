import type { Sst02FilingDataDto, TaxSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxSummaryReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getSst02FilingData = (companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Sst02FilingDataDto>({
      method: 'GET',
      url: `/api/app/tax-summary-report/sst02Filing-data/${companyId}`,
      params: { fromDate, toDate },
    },
    { apiName: this.apiName,...config });
  

  getTaxSummary = (companyId: string, fromDate: string, toDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxSummaryDto>({
      method: 'GET',
      url: `/api/app/tax-summary-report/tax-summary/${companyId}`,
      params: { fromDate, toDate },
    },
    { apiName: this.apiName,...config });
}