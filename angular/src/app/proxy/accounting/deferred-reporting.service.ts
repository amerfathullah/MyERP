import type {
  DeferredRevenueAndExpenseReportDto,
  DeferredRevenueAndExpenseRequestDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DeferredReportingService {
  private restService = inject(RestService);
  apiName = 'Default';

  getDeferredRevenueAndExpense = (
    input: DeferredRevenueAndExpenseRequestDto,
    config?: Partial<Rest.Config>,
  ) =>
    this.restService.request<any, DeferredRevenueAndExpenseReportDto>(
      {
        method: 'GET',
        headers: { Accept: 'application/json' },
        url: '/api/app/deferred-reporting/deferred-revenue-and-expense',
        params: {
          companyId: input.companyId,
          filterBasedOn: input.filterBasedOn,
          fromFiscalYearId: input.fromFiscalYearId,
          toFiscalYearId: input.toFiscalYearId,
          periodStartDate: input.periodStartDate,
          periodEndDate: input.periodEndDate,
          periodicity: input.periodicity,
          type: input.type,
          withUpcomingPostings: input.withUpcomingPostings,
        },
      },
      { apiName: this.apiName, ...config },
    );
}
