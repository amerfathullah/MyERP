import type {
  ExponentialSmoothingForecastingFilterDto,
  ExponentialSmoothingForecastingDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExponentialSmoothingForecastingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getForecast = (
    input: ExponentialSmoothingForecastingFilterDto,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, ExponentialSmoothingForecastingDto>(
      {
        method: 'GET',
        url: '/api/app/exponential-smoothing-forecasting/forecast',
        params: {
          companyId: input.companyId,
          basedOnDocument: input.basedOnDocument,
          basedOnField: input.basedOnField,
          periodicity: input.periodicity,
          fromDate: input.fromDate,
          toDate: input.toDate,
          noOfYears: input.noOfYears,
          smoothingConstant: input.smoothingConstant,
          itemId: input.itemId,
          warehouseId: input.warehouseId,
          searchTerm: input.searchTerm,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
