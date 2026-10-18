import type { ProductionPlanningReportFilterDto, ProductionPlanningReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProductionPlanningReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: ProductionPlanningReportFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductionPlanningReportDto>(
      {
        method: 'GET',
        url: '/api/app/production-planning-report/report',
        params: {
          companyId: input.companyId,
          basedOn: input.basedOn,
          rawMaterialWarehouseId: input.rawMaterialWarehouseId,
          itemId: input.itemId,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
