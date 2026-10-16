import type { BomVarianceReportDto, BomVarianceReportRequestDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BomVarianceReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: BomVarianceReportRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BomVarianceReportDto>({
      method: 'POST',
      url: '/api/app/bom-variance-report/report',
      body: input,
    },
    { apiName: this.apiName, ...config });
}
