import type { CancelEInvoiceDto, EInvoiceSubmissionDto, SubmitEInvoiceDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EInvoiceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (input: CancelEInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'POST',
      url: '/api/app/e-invoice/cancel',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EInvoiceSubmissionDto>>({
      method: 'GET',
      url: '/api/app/e-invoice',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStatus = (submissionId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'GET',
      url: `/api/app/e-invoice/status/${submissionId}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (input: SubmitEInvoiceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EInvoiceSubmissionDto>({
      method: 'POST',
      url: '/api/app/e-invoice/submit',
      body: input,
    },
    { apiName: this.apiName,...config });
}