import type { SerialNoWarrantyExpiryFilterDto, SerialNoWarrantyExpiryReportDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SerialNoWarrantyExpiryService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (input: SerialNoWarrantyExpiryFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SerialNoWarrantyExpiryReportDto>(
      {
        method: 'GET',
        url: '/api/app/serial-no-warranty-expiry/report',
        params: {
          companyId: input.companyId,
          warehouseId: input.warehouseId,
          itemId: input.itemId,
          customerId: input.customerId,
          expiryType: input.expiryType,
          maintenanceStatus: input.maintenanceStatus,
          daysThreshold: input.daysThreshold,
          fromDate: input.fromDate,
          toDate: input.toDate,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
