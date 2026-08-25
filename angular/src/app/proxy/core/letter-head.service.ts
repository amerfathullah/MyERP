import type { CreateUpdateLetterHeadDto, GetLetterHeadListDto, LetterHeadDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LetterHeadService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateLetterHeadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LetterHeadDto>({
      method: 'POST',
      url: '/api/app/letter-head',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/letter-head/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LetterHeadDto>({
      method: 'GET',
      url: `/api/app/letter-head/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: GetLetterHeadListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LetterHeadDto>>({
      method: 'GET',
      url: '/api/app/letter-head',
      params: { companyId: input.companyId, letterHeadFor: input.letterHeadFor, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  setDefault = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LetterHeadDto>({
      method: 'POST',
      url: `/api/app/letter-head/${id}/set-default`,
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: CreateUpdateLetterHeadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LetterHeadDto>({
      method: 'PUT',
      url: `/api/app/letter-head/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
