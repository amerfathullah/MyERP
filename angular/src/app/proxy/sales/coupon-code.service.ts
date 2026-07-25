import type { CouponCodeDto, CreateCouponCodeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CouponCodeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateCouponCodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CouponCodeDto>({
      method: 'POST',
      url: '/api/app/coupon-code',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/coupon-code/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CouponCodeDto>({
      method: 'GET',
      url: `/api/app/coupon-code/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CouponCodeDto>>({
      method: 'GET',
      url: '/api/app/coupon-code',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  reverseUsage = (couponCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/coupon-code/reverse-usage',
      params: { couponCode },
    },
    { apiName: this.apiName,...config });
  

  toggle = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/coupon-code/${id}/toggle`,
    },
    { apiName: this.apiName,...config });
  

  validateAndApply = (couponCode: string, customerId: string, transactionDate: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: `/api/app/coupon-code/validate-and-apply/${customerId}`,
      params: { couponCode, transactionDate },
    },
    { apiName: this.apiName,...config });
}