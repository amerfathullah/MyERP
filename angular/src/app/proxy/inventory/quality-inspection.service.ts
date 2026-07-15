import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateQualityInspectionDto, GetQualityInspectionListDto, QualityInspectionDto } from '../dtos/models';

@Injectable({
  providedIn: 'root',
})
export class QualityInspectionService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateQualityInspectionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionDto>({
      method: 'POST',
      url: '/api/app/quality-inspection',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionDto>({
      method: 'GET',
      url: `/api/app/quality-inspection/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetQualityInspectionListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityInspectionDto>>({
      method: 'GET',
      url: '/api/app/quality-inspection',
      params: { companyId: input.companyId, itemId: input.itemId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  submit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityInspectionDto>({
      method: 'POST',
      url: `/api/app/quality-inspection/${id}/submit`,
    },
    { apiName: this.apiName,...config });
}