import type { WorkOrderSummaryDto, WorkOrderSummaryRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class WorkOrderSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getSummary = (input: WorkOrderSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, WorkOrderSummaryDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/work-order-summary/summary',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, basedOn: input.basedOn, workOrderIds: input.workOrderIds, productionItemIds: input.productionItemIds, salesOrderIds: input.salesOrderIds, status: input.status, age: input.age, chartsBasedOn: input.chartsBasedOn },
    },
      { apiName: this.apiName,...config });
}
