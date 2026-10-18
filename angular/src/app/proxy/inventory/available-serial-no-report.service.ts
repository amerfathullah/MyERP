import type { AvailableSerialNoFilterDto, AvailableSerialNoReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AvailableSerialNoReportService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: AvailableSerialNoFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AvailableSerialNoReportDto>(
      {
        method: 'GET',
        url: '/api/app/available-serial-no-report/report',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
