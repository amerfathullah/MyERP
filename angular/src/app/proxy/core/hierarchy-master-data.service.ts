import type { CreateHierarchyNodeDto, HierarchyNodeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class HierarchyMasterDataService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createCustomerGroup = (input: CreateHierarchyNodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, HierarchyNodeDto>({
      method: 'POST',
      url: '/api/app/hierarchy-master-data/customer-group',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createSupplierGroup = (input: CreateHierarchyNodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, HierarchyNodeDto>({
      method: 'POST',
      url: '/api/app/hierarchy-master-data/supplier-group',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createTerritory = (input: CreateHierarchyNodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, HierarchyNodeDto>({
      method: 'POST',
      url: '/api/app/hierarchy-master-data/territory',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteCustomerGroup = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/hierarchy-master-data/${id}/customer-group`,
    },
    { apiName: this.apiName,...config });
  

  deleteSupplierGroup = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/hierarchy-master-data/${id}/supplier-group`,
    },
    { apiName: this.apiName,...config });
  

  deleteTerritory = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/hierarchy-master-data/${id}/territory`,
    },
    { apiName: this.apiName,...config });
  

  getCustomerGroups = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, HierarchyNodeDto[]>({
      method: 'GET',
      url: '/api/app/hierarchy-master-data/customer-groups',
    },
    { apiName: this.apiName,...config });
  

  getSupplierGroups = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, HierarchyNodeDto[]>({
      method: 'GET',
      url: '/api/app/hierarchy-master-data/supplier-groups',
    },
    { apiName: this.apiName,...config });
  

  getTerritories = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, HierarchyNodeDto[]>({
      method: 'GET',
      url: '/api/app/hierarchy-master-data/territories',
    },
    { apiName: this.apiName,...config });
}