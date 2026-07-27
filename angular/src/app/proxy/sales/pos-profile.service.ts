import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface PosProfileDto {
  id: string;
  companyId: string;
  profileName: string;
  warehouseId: string;
  priceListId?: string | null;
  defaultCustomerId?: string | null;
  currencyCode: string;
  validateStock: boolean;
  invoiceType: string;
  isDisabled: boolean;
  taxTemplateId?: string | null;
  writeOffAccountId?: string | null;
  writeOffLimit: number;
  postChangeGlEntries: boolean;
  paymentMethods: PosProfilePaymentMethodDto[];
}

export interface PosProfilePaymentMethodDto {
  id: string;
  modeOfPaymentId: string;
  accountId: string;
  isDefault: boolean;
}

export interface CreateUpdatePosProfileDto {
  companyId: string;
  profileName: string;
  warehouseId: string;
  priceListId?: string | null;
  defaultCustomerId?: string | null;
  currencyCode?: string;
  validateStock?: boolean;
  invoiceType?: string;
  taxTemplateId?: string | null;
  writeOffAccountId?: string | null;
  writeOffLimit?: number;
  postChangeGlEntries?: boolean;
  paymentMethods?: { modeOfPaymentId: string; accountId: string; isDefault: boolean }[];
}

@Injectable({ providedIn: 'root' })
export class PosProfileService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto & { companyId?: string }, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosProfileDto>>({
      method: 'GET',
      url: '/api/app/pos-profile',
      params: { companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'GET',
      url: `/api/app/pos-profile/${id}`,
    }, { apiName: this.apiName, ...config });

  create = (input: CreateUpdatePosProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: '/api/app/pos-profile',
      body: input,
    }, { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdatePosProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'PUT',
      url: `/api/app/pos-profile/${id}`,
      body: input,
    }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/pos-profile/${id}`,
    }, { apiName: this.apiName, ...config });

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: `/api/app/pos-profile/${id}/disable`,
    }, { apiName: this.apiName, ...config });

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: `/api/app/pos-profile/${id}/enable`,
    }, { apiName: this.apiName, ...config });
}
