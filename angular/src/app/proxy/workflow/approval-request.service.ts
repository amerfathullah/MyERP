import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

export interface ApprovalRequestDto {
  id: string;
  approvalRuleId: string;
  documentType: string;
  documentId: string;
  level: number;
  status: number;
  requestedByUserId: string;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  remarks?: string | null;
  creationTime: string;
  ruleName?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ApprovalRequestService {
  private restService = inject(RestService);
  apiName = 'Default';

  getMyPending = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ApprovalRequestDto>>({
      method: 'GET',
      url: '/api/app/approval-workflow/pending-approvals',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  getPendingCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ApprovalRequestDto>>({
      method: 'GET',
      url: '/api/app/approval-workflow/pending-approvals',
      params: { skipCount: 0, maxResultCount: 1 },
    }, { apiName: this.apiName, ...config });

  approve = (id: string, remarks?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRequestDto>({
      method: 'POST',
      url: '/api/app/approval-workflow/approve',
      body: { requestId: id, remarks },
    }, { apiName: this.apiName, ...config });

  reject = (id: string, remarks?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRequestDto>({
      method: 'POST',
      url: '/api/app/approval-workflow/reject',
      body: { requestId: id, remarks },
    }, { apiName: this.apiName, ...config });
}
