import type { DocumentConnectionsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentConnectionsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getConnections = (documentType: string, documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentConnectionsDto>({
      method: 'GET',
      url: `/api/app/document-connections/connections/${documentId}`,
      params: { documentType },
    },
    { apiName: this.apiName,...config });
}