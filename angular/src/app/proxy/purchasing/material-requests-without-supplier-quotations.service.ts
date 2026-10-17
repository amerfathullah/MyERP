import type { MaterialRequestsWithoutSupplierQuotationsFilterDto, MaterialRequestsWithoutSupplierQuotationsReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MaterialRequestsWithoutSupplierQuotationsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: MaterialRequestsWithoutSupplierQuotationsFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MaterialRequestsWithoutSupplierQuotationsReportDto>({
      method: 'GET',
      url: '/api/app/material-requests-without-supplier-quotations/report',
      params: {
        companyId: input.companyId,
        fromDate: input.fromDate,
        toDate: input.toDate,
        warehouseId: input.warehouseId,
        searchTerm: input.searchTerm,
      },
    },
    { apiName: this.apiName,...config });
}
