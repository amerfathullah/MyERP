import type { CreateSalaryStructureDto, SalaryStructureDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalaryStructureService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateSalaryStructureDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryStructureDto>({
      method: 'POST',
      url: '/api/app/salary-structure',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryStructureDto>({
      method: 'GET',
      url: `/api/app/salary-structure/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SalaryStructureDto>>({
      method: 'GET',
      url: '/api/app/salary-structure',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateSalaryStructureDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SalaryStructureDto>({
      method: 'PUT',
      url: `/api/app/salary-structure/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}