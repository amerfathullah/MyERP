import type { AutomationRuleDto, CreateAutomationRuleDto, UpdateAutomationRuleDto, AutomationExecutionLogDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AutomationRuleService {
  private restService = inject(RestService);
  apiName = 'Default';

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AutomationRuleDto>>({
      method: 'GET',
      url: '/api/app/automation-rule',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutomationRuleDto>({
      method: 'GET',
      url: `/api/app/automation-rule/${id}`,
    }, { apiName: this.apiName, ...config });

  create = (input: CreateAutomationRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutomationRuleDto>({
      method: 'POST',
      url: '/api/app/automation-rule',
      body: input,
    }, { apiName: this.apiName, ...config });

  update = (id: string, input: UpdateAutomationRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutomationRuleDto>({
      method: 'PUT',
      url: `/api/app/automation-rule/${id}`,
      body: input,
    }, { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/automation-rule/${id}`,
    }, { apiName: this.apiName, ...config });

  toggleActive = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AutomationRuleDto>({
      method: 'POST',
      url: `/api/app/automation-rule/${id}/toggle-active`,
    }, { apiName: this.apiName, ...config });

  getExecutionLogs = (ruleId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AutomationExecutionLogDto>>({
      method: 'GET',
      url: `/api/app/automation-rule/${ruleId}/execution-logs`,
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    }, { apiName: this.apiName, ...config });
}
