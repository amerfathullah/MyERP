import type { CreateQualityFeedbackDto, CreateQualityReviewDto, CreateUpdateNonConformanceDto, CreateUpdateQualityActionDto, CreateUpdateQualityFeedbackTemplateDto, CreateUpdateQualityGoalDto, CreateUpdateQualityMeetingDto, CreateUpdateQualityProcedureDto, EvaluateGoalDto, EvaluateQualityReviewDto, NonConformanceDto, QualityActionDto, QualityFeedbackDto, QualityFeedbackTemplateDto, QualityGoalDto, QualityMeetingDto, QualityProcedureDto, QualityReviewDto, ResolveNonConformanceDto, ResolveQualityActionDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class QualityManagementService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  cancelNonConformance = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NonConformanceDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/cancel-non-conformance`,
    },
    { apiName: this.apiName,...config });
  

  closeAction = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityActionDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/close-action`,
    },
    { apiName: this.apiName,...config });
  

  closeMeeting = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityMeetingDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/close-meeting`,
    },
    { apiName: this.apiName,...config });
  

  createAction = (input: CreateUpdateQualityActionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityActionDto>({
      method: 'POST',
      url: '/api/app/quality-management/action',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createFeedback = (input: CreateQualityFeedbackDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityFeedbackDto>({
      method: 'POST',
      url: '/api/app/quality-management/feedback',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createFeedbackTemplate = (input: CreateUpdateQualityFeedbackTemplateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityFeedbackTemplateDto>({
      method: 'POST',
      url: '/api/app/quality-management/feedback-template',
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
  

  createMeeting = (input: CreateUpdateQualityMeetingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityMeetingDto>({
      method: 'POST',
      url: '/api/app/quality-management/meeting',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createNonConformance = (input: CreateUpdateNonConformanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NonConformanceDto>({
      method: 'POST',
      url: '/api/app/quality-management/non-conformance',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createProcedure = (input: CreateUpdateQualityProcedureDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityProcedureDto>({
      method: 'POST',
      url: '/api/app/quality-management/procedure',
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
  

  deleteProcedure = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/quality-management/${id}/procedure`,
    },
    { apiName: this.apiName,...config });
  

  evaluateGoal = (id: string, input: EvaluateGoalDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityReviewDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/evaluate-goal`,
      body: input,
    },
    { apiName: this.apiName,...config });


  evaluateReview = (id: string, input: EvaluateQualityReviewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityReviewDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/evaluate-review`,
      body: input,
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
  

  getFeedback = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityFeedbackDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/feedback`,
    },
    { apiName: this.apiName,...config });
  

  getFeedbackList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityFeedbackDto>>({
      method: 'GET',
      url: '/api/app/quality-management/feedback-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getFeedbackTemplate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityFeedbackTemplateDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/feedback-template`,
    },
    { apiName: this.apiName,...config });
  

  getFeedbackTemplateList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityFeedbackTemplateDto>>({
      method: 'GET',
      url: '/api/app/quality-management/feedback-template-list',
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
  

  getMeeting = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityMeetingDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/meeting`,
    },
    { apiName: this.apiName,...config });
  

  getMeetingList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityMeetingDto>>({
      method: 'GET',
      url: '/api/app/quality-management/meeting-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getNonConformance = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NonConformanceDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/non-conformance`,
    },
    { apiName: this.apiName,...config });
  

  getNonConformanceList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NonConformanceDto>>({
      method: 'GET',
      url: '/api/app/quality-management/non-conformance-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getProcedure = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityProcedureDto>({
      method: 'GET',
      url: `/api/app/quality-management/${id}/procedure`,
    },
    { apiName: this.apiName,...config });
  

  getProcedureList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<QualityProcedureDto>>({
      method: 'GET',
      url: '/api/app/quality-management/procedure-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getProcedureTree = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityProcedureDto[]>({
      method: 'GET',
      url: '/api/app/quality-management/procedure-tree',
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
  

  resolveNonConformance = (id: string, input: ResolveNonConformanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NonConformanceDto>({
      method: 'POST',
      url: `/api/app/quality-management/${id}/resolve-non-conformance`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateAction = (id: string, input: CreateUpdateQualityActionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityActionDto>({
      method: 'PUT',
      url: `/api/app/quality-management/${id}/action`,
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
  

  updateNonConformance = (id: string, input: CreateUpdateNonConformanceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NonConformanceDto>({
      method: 'PUT',
      url: `/api/app/quality-management/${id}/non-conformance`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateProcedure = (id: string, input: CreateUpdateQualityProcedureDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, QualityProcedureDto>({
      method: 'PUT',
      url: `/api/app/quality-management/${id}/procedure`,
      body: input,
    },
    { apiName: this.apiName,...config });
}