import type {
  CreateRepostAccountingLedgerDto,
  RepostAccountingLedgerDto,
  RepostableVoucherDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class RepostAccountingLedgerService {
  private restService = inject(RestService);
  apiName = 'Default';


  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RepostAccountingLedgerDto>>({
      method: 'GET',
      url: '/api/app/repost-accounting-ledger',
      params: { companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostAccountingLedgerDto>({
      method: 'GET',
      url: `/api/app/repost-accounting-ledger/${id}`,
    },
    { apiName: this.apiName, ...config });


  create = (input: CreateRepostAccountingLedgerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostAccountingLedgerDto>({
      method: 'POST',
      url: '/api/app/repost-accounting-ledger',
      body: input,
    },
    { apiName: this.apiName, ...config });


  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostAccountingLedgerDto>({
      method: 'POST',
      url: `/api/app/repost-accounting-ledger/${id}/submit`,
    },
    { apiName: this.apiName, ...config });


  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostAccountingLedgerDto>({
      method: 'POST',
      url: `/api/app/repost-accounting-ledger/${id}/cancel`,
    },
    { apiName: this.apiName, ...config });


  getAllowedVoucherTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: '/api/app/repost-accounting-ledger/allowed-voucher-types',
    },
    { apiName: this.apiName, ...config });


  resolveVoucher = (voucherType: string, voucherNumber: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RepostableVoucherDto>({
      method: 'GET',
      url: '/api/app/repost-accounting-ledger/resolve-voucher',
      params: { voucherType, voucherNumber },
    },
    { apiName: this.apiName, ...config });
}
