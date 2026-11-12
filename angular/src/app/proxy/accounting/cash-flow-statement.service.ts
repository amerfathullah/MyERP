import type { CashFlowRequestDto, CashFlowStatementDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CashFlowStatementService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getCashFlowStatement = (input: CashFlowRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashFlowStatementDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/cash-flow-statement/cash-flow-statement',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, periodicity: input.periodicity, accumulatedValues: input.accumulatedValues, showOpeningAndClosingBalance: input.showOpeningAndClosingBalance },
    },
      { apiName: this.apiName,...config });
}
