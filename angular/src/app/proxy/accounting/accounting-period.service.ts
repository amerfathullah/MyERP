import type { AccountingPeriodDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AccountingPeriodService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AccountingPeriodDto>({
      method: 'POST',
      url: `/api/app/accounting-period/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AccountingPeriodDto>>({
      method: 'GET',
      url: '/api/app/accounting-period',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}