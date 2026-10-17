import type { QualityInspectionSummaryReportDto, QualityInspectionSummaryRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QualityInspectionSummaryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: QualityInspectionSummaryRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionSummaryReportDto>({
      method: 'POST',
      url: '/api/app/quality-inspection-summary/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
