import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface ApprovalRequestDto {
  id: string;
  documentType?: string;
  documentId?: string;
  documentNumber?: string;
  requestedByUserId?: string;
  requestedByUserName?: string | null;
  status?: number;
  creationTime?: string;
  notes?: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class ApprovalRequestService {
  private restService = inject(RestService);
  apiName = 'Default';

  getPendingCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, { totalCount: number }>({
      method: 'GET',
      url: '/api/app/approval-request/pending-count',
    }, { apiName: this.apiName, ...config });

  getMyPending = (input: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ApprovalRequestDto>>({
      method: 'GET',
      url: '/api/app/approval-request/my-pending',
      params: input,
    }, { apiName: this.apiName, ...config });

  approve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRequestDto>({
      method: 'POST',
      url: `/api/app/approval-request/${id}/approve`,
    }, { apiName: this.apiName, ...config });

  reject = (id: string, reason?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRequestDto>({
      method: 'POST',
      url: `/api/app/approval-request/${id}/reject`,
      params: { reason },
    }, { apiName: this.apiName, ...config });
}
