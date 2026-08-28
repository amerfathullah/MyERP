import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CalculateLandedCostDistributionDto, CreateLandedCostVoucherDto, GetLandedCostReceiptItemsInput, GetLandedCostVoucherListDto, LandedCostDistributionResultDto, LandedCostItemDto, LandedCostVoucherDto } from '../dtos/models';

@Injectable({
  providedIn: 'root',
})
export class LandedCostVoucherService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  calculateDistribution = (input: CalculateLandedCostDistributionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostDistributionResultDto>({
      method: 'POST',
      url: '/api/app/landed-cost-voucher/calculate-distribution',
      body: input,
    },
    { apiName: this.apiName,...config });


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostVoucherDto>({
      method: 'POST',
      url: `/api/app/landed-cost-voucher/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateLandedCostVoucherDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostVoucherDto>({
      method: 'POST',
      url: '/api/app/landed-cost-voucher',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostVoucherDto>({
      method: 'GET',
      url: `/api/app/landed-cost-voucher/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetLandedCostVoucherListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LandedCostVoucherDto>>({
      method: 'GET',
      url: '/api/app/landed-cost-voucher',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getReceiptItems = (input: GetLandedCostReceiptItemsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostItemDto[]>({
      method: 'GET',
      url: '/api/app/landed-cost-voucher/receipt-items',
      params: { companyId: input.companyId, receiptType: input.receiptType, receiptIds: input.receiptIds },
    },
    { apiName: this.apiName,...config });


  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LandedCostVoucherDto>({
      method: 'POST',
      url: `/api/app/landed-cost-voucher/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}