import type { CogsByItemGroupFilterDto, CogsByItemGroupReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CogsByItemGroupService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: CogsByItemGroupFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CogsByItemGroupReportDto>({
      method: 'GET',
      url: '/api/app/cogs-by-item-group/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        warehouseId: input.warehouseId,
      },
    },
    { apiName: this.apiName, ...config });
}
