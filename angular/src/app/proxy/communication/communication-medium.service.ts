import type { CommunicationMediumDto, CreateUpdateCommunicationMediumDto, GetCommunicationMediumListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CommunicationMediumService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateCommunicationMediumDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CommunicationMediumDto>({
      method: 'POST',
      url: '/api/app/communication-medium',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/communication-medium/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CommunicationMediumDto>({
      method: 'GET',
      url: `/api/app/communication-medium/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getHandlingEmployeeGroup = (id: string, dayOfWeek: any, time: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'GET',
      responseType: 'text',
      url: `/api/app/communication-medium/${id}/handling-employee-group`,
      params: { dayOfWeek, time },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetCommunicationMediumListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CommunicationMediumDto>>({
      method: 'GET',
      url: '/api/app/communication-medium',
      params: { communicationMediumType: input.communicationMediumType, isDisabled: input.isDisabled, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateCommunicationMediumDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CommunicationMediumDto>({
      method: 'PUT',
      url: `/api/app/communication-medium/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}