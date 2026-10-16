import type {
  GetTerritoryWiseSalesInput,
  TerritoryWiseSalesReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TerritoryWiseSalesService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetTerritoryWiseSalesInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, TerritoryWiseSalesReportDto>(
      {
        method: 'GET',
        url: '/api/app/territory-wise-sales/report',
        params: {
          companyId: input.companyId,
          fromDate: input.fromDate,
          toDate: input.toDate,
          territoryId: input.territoryId,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
