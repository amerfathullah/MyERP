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
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/quality-inspection-summary/report',
      params: { companyId: input.companyId, fromDate: input.fromDate, toDate: input.toDate, status: input.status, itemId: input.itemId, inspectionType: input.inspectionType, inspectedById: input.inspectedById },
    },
      { apiName: this.apiName,...config });
}
