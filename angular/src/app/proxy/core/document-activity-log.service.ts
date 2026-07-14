import type { DocumentActivityLogDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DocumentActivityLogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getForDocument = (documentType: string, documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentActivityLogDto[]>({
      method: 'GET',
      url: `/api/app/document-activity-log/for-document/${documentId}`,
      params: { documentType },
    },
    { apiName: this.apiName,...config });
  

  getRecent = (companyId: string, skipCount?: number, maxResultCount: number = 20, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DocumentActivityLogDto>>({
      method: 'GET',
      url: `/api/app/document-activity-log/recent/${companyId}`,
      params: { skipCount, maxResultCount },
    },
    { apiName: this.apiName,...config });
}