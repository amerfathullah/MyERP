import type { MaterialRequirementsPlanningFilterDto, MaterialRequirementsPlanningReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaterialRequirementsPlanningService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: MaterialRequirementsPlanningFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequirementsPlanningReportDto>({
      method: 'GET',
      url: '/api/app/manufacturing/mrp-report',
      params: input,
    },
    { apiName: this.apiName, ...config });
}
