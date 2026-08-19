import type { CreateUpdateDrivingLicenseCategoryDto, DrivingLicenseCategoryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DrivingLicenseCategoryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateDrivingLicenseCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DrivingLicenseCategoryDto>({
      method: 'POST',
      url: '/api/app/driving-license-category',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/driving-license-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DrivingLicenseCategoryDto>({
      method: 'GET',
      url: `/api/app/driving-license-category/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DrivingLicenseCategoryDto>>({
      method: 'GET',
      url: '/api/app/driving-license-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateDrivingLicenseCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DrivingLicenseCategoryDto>({
      method: 'PUT',
      url: `/api/app/driving-license-category/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}