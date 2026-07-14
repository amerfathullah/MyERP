import type { ContactDto, CreateContactDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ContactService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateContactDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ContactDto>({
      method: 'POST',
      url: '/api/app/contact',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/contact/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (partyType: string, partyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ContactDto>>({
      method: 'GET',
      url: '/api/app/contact',
      params: { partyType, partyId },
    },
    { apiName: this.apiName,...config });
}