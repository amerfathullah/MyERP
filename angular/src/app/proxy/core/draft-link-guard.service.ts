import type { DraftLinkDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DraftLinkGuardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getExistingDrafts = (sourceDocType: string, sourceId: string, targetDocType: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DraftLinkDto[]>({
      method: 'GET',
      url: `/api/app/draft-link-guard/existing-drafts/${sourceId}`,
      params: { sourceDocType, targetDocType },
    },
    { apiName: this.apiName,...config });
}