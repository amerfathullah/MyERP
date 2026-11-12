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
      headers: { Accept: 'application/json' },
      url: '/api/app/budget-variance-report/report',
      params: { companyId: input.companyId, fiscalYearId: input.fiscalYearId, fromDate: input.fromDate, toDate: input.toDate, costCenterId: input.costCenterId, projectId: input.projectId, budgetAgainst: input.budgetAgainst, budgetAgainstId: input.budgetAgainstId, financeBook: input.financeBook },
    },
      { apiName: this.apiName,...config });
}
