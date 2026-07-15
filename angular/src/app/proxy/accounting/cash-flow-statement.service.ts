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
      url: '/api/app/cash-flow-statement/cash-flow-statement',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate },
    },
    { apiName: this.apiName,...config });
}