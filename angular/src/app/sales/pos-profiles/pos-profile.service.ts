import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface PosProfileDto {
  id?: string;
  companyId?: string;
  profileName?: string;
  warehouseId?: string | null;
  currencyCode?: string | null;
  invoiceType?: string | null;
  validateStock?: boolean;
  writeOffLimit?: number;
  postChangeGlEntries?: boolean;
  isEnabled?: boolean;
  paymentMethods?: PosProfilePaymentMethodDto[];
}

export interface PosProfilePaymentMethodDto {
  modeOfPaymentId?: string;
  accountId?: string;
  isDefault?: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class PosProfileService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: '/api/app/pos-profile',
      body: input,
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'GET',
      url: `/api/app/pos-profile/${id}`,
    }, { apiName: this.apiName, ...config });

  getList = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PosProfileDto>>({
      method: 'GET',
      url: '/api/app/pos-profile',
      params: input,
    }, { apiName: this.apiName, ...config });

  update = (id: string, input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'PUT',
      url: `/api/app/pos-profile/${id}`,
      body: input,
    }, { apiName: this.apiName, ...config });

  enable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: `/api/app/pos-profile/${id}/enable`,
    }, { apiName: this.apiName, ...config });

  disable = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PosProfileDto>({
      method: 'POST',
      url: `/api/app/pos-profile/${id}/disable`,
    }, { apiName: this.apiName, ...config });
}
