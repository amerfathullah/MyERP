import type { CreateUpdatePlantFloorDto, GetPlantFloorListDto, PlantFloorDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PlantFloorService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdatePlantFloorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PlantFloorDto>({
      method: 'POST',
      url: '/api/app/plant-floor',
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/plant-floor/${id}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PlantFloorDto>({
      method: 'GET',
      url: `/api/app/plant-floor/${id}`,
    },
    { apiName: this.apiName, ...config });

  getAllList = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PlantFloorDto[]>({
      method: 'GET',
      url: `/api/app/plant-floor/all-list/${companyId}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: GetPlantFloorListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PlantFloorDto>>({
      method: 'GET',
      url: '/api/app/plant-floor',
      params: { companyId: input.companyId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdatePlantFloorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PlantFloorDto>({
      method: 'PUT',
      url: `/api/app/plant-floor/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
