import type { JobCardSummaryDto, JobCardSummaryRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class JobCardSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getSummary = (input: JobCardSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobCardSummaryDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/job-card-summary/summary',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, workOrderIds: input.workOrderIds, productionItemIds: input.productionItemIds, workstationId: input.workstationId, operationId: input.operationId, status: input.status },
    },
      { apiName: this.apiName,...config });
}
