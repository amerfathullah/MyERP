import type { DocumentConnectionsDto, ExistingDraftDto } from './models';
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
  

  getExistingDrafts = (sourceDocType: string, sourceId: string, targetDocType: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExistingDraftDto[]>({
      method: 'GET',
      url: `/api/app/document-connections/existing-drafts/${sourceId}`,
      params: { sourceDocType, targetDocType },
    },
    { apiName: this.apiName,...config });
}