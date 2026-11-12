import type { ProcessLossReportDto, ProcessLossReportRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProcessLossReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: ProcessLossReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProcessLossReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/process-loss-report/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, itemId: input.itemId, workOrderId: input.workOrderId },
    },
      { apiName: this.apiName,...config });
}
