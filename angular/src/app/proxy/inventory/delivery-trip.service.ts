import type { CreateUpdateDeliveryTripDto, DeliveryTripDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DeliveryTripService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancel = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'POST',
      url: `/api/app/delivery-trip/${id}/cancel`,
    },
    { apiName: this.apiName,...config });
  

  complete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'POST',
      url: `/api/app/delivery-trip/${id}/complete`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateDeliveryTripDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'POST',
      url: '/api/app/delivery-trip',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/delivery-trip/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'GET',
      url: `/api/app/delivery-trip/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DeliveryTripDto>>({
      method: 'GET',
      url: '/api/app/delivery-trip',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  schedule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'POST',
      url: `/api/app/delivery-trip/${id}/schedule`,
    },
    { apiName: this.apiName,...config });
  

  startTransit = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'POST',
      url: `/api/app/delivery-trip/${id}/start-transit`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateDeliveryTripDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeliveryTripDto>({
      method: 'PUT',
      url: `/api/app/delivery-trip/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}