import type { GetTerritoryTargetVarianceInput, TerritoryTargetVarianceReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TerritoryTargetVarianceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: GetTerritoryTargetVarianceInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TerritoryTargetVarianceReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/territory-target-variance/report',
      params: { companyId: input.companyId, docType: input.docType, targetOn: input.targetOn, fiscalYearId: input.fiscalYearId, fromDate: input.fromDate, toDate: input.toDate, territoryId: input.territoryId, itemGroupId: input.itemGroupId },
    },
      { apiName: this.apiName,...config });
}
