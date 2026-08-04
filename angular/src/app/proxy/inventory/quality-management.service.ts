import type { CreateQualityReviewDto, CreateUpdateQualityActionDto, CreateUpdateQualityGoalDto, QualityActionDto, QualityGoalDto, QualityReviewDto, ResolveQualityActionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QualityManagementService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  createAction = (input: CreateUpdateQualityActionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityActionDto>({
      method: 'POST',
      url: '/api/app/quality-management/action',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createGoal = (input: CreateUpdateQualityGoalDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityGoalDto>({
      method: 'POST',
      url: '/api/app/quality-management/goal',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createReview = (input: CreateQualityReviewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityReviewDto>({
      method: 'POST',
      url: '/api/app/quality-management/review',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteGoal = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quality-management/${id}/goal`,
    },
    { apiName: this.apiName,...config });
  

  getAction = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityActionDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/action`,
    },
    { apiName: this.apiName,...config });
  

  getActionList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityActionDto>>({
      method: 'GET',
      url: '/api/app/quality-management/action-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getGoal = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityGoalDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/goal`,
    },
    { apiName: this.apiName,...config });
  

  getGoalList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityGoalDto>>({
      method: 'GET',
      url: '/api/app/quality-management/goal-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getReview = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityReviewDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/review`,
    },
    { apiName: this.apiName,...config });
  

  getReviewList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityReviewDto>>({
      method: 'GET',
      url: '/api/app/quality-management/review-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  resolveAction = (id: string, input: ResolveQualityActionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityActionDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/resolve-action`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateGoal = (id: string, input: CreateUpdateQualityGoalDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityGoalDto>({
      method: 'PUT',
      url: `/api/app/quality-management/${id}/goal`,
      body: input,
    },
    { apiName: this.apiName,...config });
}