import type { CreateUpdateQualityInspectionParameterGroupDto, GetQualityInspectionParameterGroupListDto, QualityInspectionParameterGroupDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QualityInspectionParameterGroupService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateQualityInspectionParameterGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionParameterGroupDto>({
      method: 'POST',
      url: '/api/app/quality-inspection-parameter-group',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quality-inspection-parameter-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionParameterGroupDto>({
      method: 'GET',
      url: `/api/app/quality-inspection-parameter-group/${id}`,
    },
    { apiName: this.apiName, ...config });

  getAllList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionParameterGroupDto[]>({
      method: 'GET',
      url: '/api/app/quality-inspection-parameter-group/all-list',
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetQualityInspectionParameterGroupListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityInspectionParameterGroupDto>>({
      method: 'GET',
      url: '/api/app/quality-inspection-parameter-group',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateQualityInspectionParameterGroupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionParameterGroupDto>({
      method: 'PUT',
      url: `/api/app/quality-inspection-parameter-group/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
