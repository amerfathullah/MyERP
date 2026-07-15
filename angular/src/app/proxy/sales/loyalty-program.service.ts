import type { CreateLoyaltyProgramDto, LoyaltyBalanceDto, LoyaltyPointEntryDto, LoyaltyProgramDto, UpdateLoyaltyProgramDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LoyaltyProgramService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateLoyaltyProgramDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoyaltyProgramDto>({
      method: 'POST',
      url: '/api/app/loyalty-program',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/loyalty-program/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoyaltyProgramDto>({
      method: 'GET',
      url: `/api/app/loyalty-program/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCustomerBalance = (customerId: string, programId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoyaltyBalanceDto>({
      method: 'GET',
      url: '/api/app/loyalty-program/customer-balance',
      params: { customerId, programId },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LoyaltyProgramDto>>({
      method: 'GET',
      url: '/api/app/loyalty-program',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPointHistory = (customerId: string, programId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoyaltyPointEntryDto[]>({
      method: 'GET',
      url: '/api/app/loyalty-program/point-history',
      params: { customerId, programId },
    },
    { apiName: this.apiName,...config });
  

  redeemPoints = (customerId: string, programId: string, pointsToRedeem: number, companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'POST',
      url: '/api/app/loyalty-program/redeem-points',
      params: { customerId, programId, pointsToRedeem, companyId },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateLoyaltyProgramDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LoyaltyProgramDto>({
      method: 'PUT',
      url: `/api/app/loyalty-program/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}