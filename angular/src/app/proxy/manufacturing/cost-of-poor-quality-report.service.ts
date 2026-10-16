import type { CostOfPoorQualityReportDto, CostOfPoorQualityReportRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CostOfPoorQualityReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: CostOfPoorQualityReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CostOfPoorQualityReportDto>({
      method: 'POST',
      url: '/api/app/cost-of-poor-quality-report/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
