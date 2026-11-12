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
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/bom-variance-report/report',
      params: { companyId: input.companyId, bomId: input.bomId, workOrderId: input.workOrderId, onlyOverproduced: input.onlyOverproduced, fromDate: input.fromDate, toDate: input.toDate },
    },
      { apiName: this.apiName,...config });
}
