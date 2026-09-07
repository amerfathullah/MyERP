import type { AddCrmNoteDto, ConvertLeadToCustomerDto, ConvertLeadToOpportunityDto, CreateLeadDto, CreateProspectAndContactDto, CreateProspectAndContactResultDto, CrmNoteDto, GetLeadListDto, LeadDto, OpportunityDto, UpdateCrmNoteDto, UpdateLeadDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LeadService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addNote = (id: string, input: AddCrmNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmNoteDto>({
      method: 'POST',
      url: `/api/app/lead/${id}/note`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  convertToCustomer = (input: ConvertLeadToCustomerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      responseType: 'text',
      url: '/api/app/lead/convert-to-customer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  convertToOpportunity = (input: ConvertLeadToOpportunityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: '/api/app/lead/convert-to-opportunity',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateLeadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadDto>({
      method: 'POST',
      url: '/api/app/lead',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createProspectAndContact = (id: string, input: CreateProspectAndContactDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CreateProspectAndContactResultDto>({
      method: 'POST',
      url: `/api/app/lead/${id}/prospect-and-contact`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/lead/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteNote = (id: string, noteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/lead/${id}/note/${noteId}`,
    },
    { apiName: this.apiName,...config });
  

  editNote = (id: string, noteId: string, input: UpdateCrmNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmNoteDto>({
      method: 'POST',
      url: `/api/app/lead/${id}/edit-note/${noteId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadDto>({
      method: 'GET',
      url: `/api/app/lead/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetLeadListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LeadDto>>({
      method: 'GET',
      url: '/api/app/lead',
      params: { status: input.status, source: input.source, filter: input.filter, companyId: input.companyId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getNotes = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmNoteDto[]>({
      method: 'GET',
      url: `/api/app/lead/${id}/notes`,
    },
    { apiName: this.apiName,...config });
  

  markLost = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadDto>({
      method: 'POST',
      url: `/api/app/lead/${id}/mark-lost`,
    },
    { apiName: this.apiName,...config });
  

  qualify = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadDto>({
      method: 'POST',
      url: `/api/app/lead/${id}/qualify`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateLeadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LeadDto>({
      method: 'PUT',
      url: `/api/app/lead/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}