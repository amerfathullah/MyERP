import type { BudgetVarianceReportDto, BudgetVarianceRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BudgetVarianceReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (input: BudgetVarianceRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BudgetVarianceReportDto>({
      method: 'GET',
      url: '/api/app/budget-variance-report/report',
      params: { companyId: input.companyId, fiscalYearId: input.fiscalYearId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}