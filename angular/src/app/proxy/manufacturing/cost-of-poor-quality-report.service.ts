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
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/cost-of-poor-quality-report/report',
      params: { companyId: input.companyId, workOrderId: input.workOrderId, operationId: input.operationId, workstationId: input.workstationId, productionItemId: input.productionItemId, fromDate: input.fromDate, toDate: input.toDate },
    },
      { apiName: this.apiName,...config });
}
