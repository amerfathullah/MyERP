import type { AddQuotationLostReasonDetailDto, CreateQuotationDto, MarkQuotationLostDto, QuotationDto, QuotationLostReasonDetailDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AddCompetitorDetailDto, CompetitorDetailDto } from '../crm/models';
import type { CompanyFilteredPagedRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class QuotationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  amend = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}/amend`,
    },
      { apiName: this.apiName,...config });
  

  
  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}/cancel`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: CreateQuotationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/quotation',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createCompetitor = (input: AddCompetitorDetailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetitorDetailDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/quotation/competitor',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createLostReason = (input: AddQuotationLostReasonDetailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationLostReasonDetailDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/quotation/lost-reason',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createRevision = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}/revision`,
    },
      { apiName: this.apiName,...config });
  

  
  deleteCompetitor = (competitorDetailId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quotation/competitor/${competitorDetailId}`,
    },
      { apiName: this.apiName,...config });
  

  
  deleteLostReason = (quotationLostReasonDetailId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quotation/lost-reason/${quotationLostReasonDetailId}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: CompanyFilteredPagedRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QuotationDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/app/quotation',
      params: { companyId: input.companyId, filter: input.filter, status: input.status, fromDate: input.fromDate, toDate: input.toDate, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  markLost = (id: string, input?: MarkQuotationLostDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}/mark-lost`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}/submit`,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: CreateQuotationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QuotationDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/app/quotation/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
