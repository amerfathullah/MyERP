import type { ApprovalRuleDto, ApprovalRequestDto, CreateApprovalRuleDto, UpdateApprovalRuleDto, ReviewApprovalDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ApprovalWorkflowService {
  private restService = inject(RestService);
  apiName = 'Default';

  getRules = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ApprovalRuleDto>>({
      method: 'GET',
      url: '/api/app/approval-workflow/rules',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  getRule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRuleDto>({
      method: 'GET',
      url: `/api/app/approval-workflow/rule/${id}`,
    }, { apiName: this.apiName, ...config });

  createRule = (input: CreateApprovalRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRuleDto>({
      method: 'POST',
      url: '/api/app/approval-workflow/rule',
      body: input,
    }, { apiName: this.apiName, ...config });

  updateRule = (id: string, input: UpdateApprovalRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRuleDto>({
      method: 'PUT',
      url: `/api/app/approval-workflow/rule/${id}`,
      body: input,
    }, { apiName: this.apiName, ...config });

  deleteRule = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/approval-workflow/rule/${id}`,
    }, { apiName: this.apiName, ...config });

  getPendingApprovals = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ApprovalRequestDto>>({
      method: 'GET',
      url: '/api/app/approval-workflow/pending-approvals',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  approve = (input: ReviewApprovalDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRequestDto>({
      method: 'POST',
      url: '/api/app/approval-workflow/approve',
      body: input,
    }, { apiName: this.apiName, ...config });

  reject = (input: ReviewApprovalDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ApprovalRequestDto>({
      method: 'POST',
      url: '/api/app/approval-workflow/reject',
      body: input,
    }, { apiName: this.apiName, ...config });
}
