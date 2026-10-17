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
      method: 'POST',
      url: '/api/app/process-loss-report/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
