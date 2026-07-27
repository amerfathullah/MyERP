import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface ProspectDto {
  id: string;
  prospectName: string;
  industry?: string | null;
  website?: string | null;
  notes?: string | null;
  isConverted: boolean;
  leadCount: number;
  opportunityCount: number;
}

export interface CreateUpdateProspectDto {
  prospectName: string;
  industry?: string | null;
  website?: string | null;
  notes?: string | null;
}

export interface GetProspectListDto extends PagedAndSortedResultRequestDto {
  filter?: string;
}

@Injectable({
  providedIn: 'root',
})
export class ProspectService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: GetProspectListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProspectDto>>({
      method: 'GET',
      url: '/api/app/prospect',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'GET',
      url: `/api/app/prospect/${id}`,
    },
    { apiName: this.apiName, ...config });

  create = (input: CreateUpdateProspectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'POST',
      url: '/api/app/prospect',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateProspectDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'PUT',
      url: `/api/app/prospect/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/prospect/${id}`,
    },
    { apiName: this.apiName, ...config });

  convert = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProspectDto>({
      method: 'POST',
      url: `/api/app/prospect/${id}/convert`,
    },
    { apiName: this.apiName, ...config });
}
