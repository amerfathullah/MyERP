import type { CalculateCashierClosingTotalsRequestDto, CalculateCashierClosingTotalsResponseDto, CashierClosingDto, CashierClosingGetListInput, CreateCashierClosingDto, UpdateCashierClosingDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CashierClosingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  calculateShiftTotals = (input: CalculateCashierClosingTotalsRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CalculateCashierClosingTotalsResponseDto>({
      method: 'POST',
      url: '/api/app/cashier-closing/calculate-shift-totals',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateCashierClosingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashierClosingDto>({
      method: 'POST',
      url: '/api/app/cashier-closing',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/cashier-closing/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashierClosingDto>({
      method: 'GET',
      url: `/api/app/cashier-closing/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CashierClosingGetListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CashierClosingDto>>({
      method: 'GET',
      url: '/api/app/cashier-closing',
      params: { filter: input.filter, fromDate: input.fromDate, toDate: input.toDate, userId: input.userId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashierClosingDto>({
      method: 'POST',
      url: `/api/app/cashier-closing/${id}/submit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateCashierClosingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CashierClosingDto>({
      method: 'PUT',
      url: `/api/app/cashier-closing/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}