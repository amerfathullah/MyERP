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
      method: 'POST',
      url: '/api/app/work-order-summary/summary',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
