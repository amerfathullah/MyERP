import type { InactiveSalesItemsFilterDto, InactiveSalesItemsReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class InactiveSalesItemsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getReport = (input: InactiveSalesItemsFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InactiveSalesItemsReportDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/inactive-sales-items/report',
      params: { companyId: input.companyId, docType: input.docType, daysSinceLastOrder: input.daysSinceLastOrder, itemGroupId: input.itemGroupId, itemId: input.itemId, territoryId: input.territoryId, asOfDate: input.asOfDate },
    },
      { apiName: this.apiName,...config });
}
