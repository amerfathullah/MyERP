import type { DowntimeAnalysisReportDto, DowntimeAnalysisRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DowntimeAnalysisService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: DowntimeAnalysisRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DowntimeAnalysisReportDto>({
      method: 'POST',
      url: '/api/app/downtime-analysis/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
