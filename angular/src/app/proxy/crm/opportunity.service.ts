import type { AddCompetitorDetailDto, AddCrmNoteDto, CompetitorDetailDto, CreateOpportunityDto, CrmNoteDto, GetOpportunityListDto, OpportunityDto, UpdateCrmNoteDto, UpdateOpportunityDto, UpdateOpportunityStageDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OpportunityService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addNote = (id: string, input: AddCrmNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmNoteDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/note`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  close = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/close`,
    },
    { apiName: this.apiName,...config });
  

  convert = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/convert`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateOpportunityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: '/api/app/opportunity',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createCompetitor = (input: AddCompetitorDetailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetitorDetailDto>({
      method: 'POST',
      url: '/api/app/opportunity/competitor',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  declareLost = (id: string, reason: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/declare-lost`,
      params: { reason },
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/opportunity/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteCompetitor = (competitorDetailId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/opportunity/competitor/${competitorDetailId}`,
    },
    { apiName: this.apiName,...config });
  

  deleteNote = (id: string, noteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/opportunity/${id}/note/${noteId}`,
    },
    { apiName: this.apiName,...config });
  

  editNote = (id: string, noteId: string, input: UpdateCrmNoteDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmNoteDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/edit-note/${noteId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'GET',
      url: `/api/app/opportunity/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetOpportunityListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OpportunityDto>>({
      method: 'GET',
      url: '/api/app/opportunity',
      params: { status: input.status, opportunityType: input.opportunityType, filter: input.filter, companyId: input.companyId, leadId: input.leadId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getNotes = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CrmNoteDto[]>({
      method: 'GET',
      url: `/api/app/opportunity/${id}/notes`,
    },
    { apiName: this.apiName,...config });
  

  markQuotation = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/mark-quotation`,
    },
    { apiName: this.apiName,...config });
  

  reopen = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'POST',
      url: `/api/app/opportunity/${id}/reopen`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateOpportunityDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'PUT',
      url: `/api/app/opportunity/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateStage = (id: string, input: UpdateOpportunityStageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OpportunityDto>({
      method: 'PUT',
      url: `/api/app/opportunity/${id}/stage`,
      body: input,
    },
    { apiName: this.apiName,...config });
}