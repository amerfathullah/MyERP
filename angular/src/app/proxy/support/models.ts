import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IssueStatus } from './issue-status.enum';
import type { AgreementStatus } from './agreement-status.enum';

export interface CreateIssueDto {
  companyId: string;
  subject: string;
  description?: string | null;
  priority?: string | null;
  issueType?: string | null;
  customerId?: string | null;
  raisedVia?: string | null;
}

export interface CreateServiceDayDto {
  dayOfWeek?: any;
  startTime?: string;
  endTime?: string;
}

export interface CreateServiceLevelAgreementDto {
  companyId: string;
  name: string;
  entityType?: string | null;
  entityId?: string | null;
  holidayListId?: string | null;
  resolutionTimeHours?: number;
  responseTimeHours?: number;
  isDefault?: boolean;
  applyOnResolution?: boolean;
  priorities?: CreateServiceLevelPriorityDto[];
  serviceDays?: CreateServiceDayDto[];
}

export interface CreateServiceLevelPriorityDto {
  priorityName: string;
  responseTimeHours?: number;
  resolutionTimeHours?: number;
  isDefault?: boolean;
}

export interface CreateUpdateIssuePriorityDto {
  name: string;
  description?: string | null;
}

export interface CreateUpdateIssueTypeDto {
  name: string;
  description?: string | null;
}

export interface GetIssueListDto extends PagedAndSortedResultRequestDto {
  status?: IssueStatus | null;
  companyId?: string | null;
  filter?: string | null;
  priority?: string | null;
}

export interface GetIssuePriorityListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetIssueTypeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetServiceLevelAgreementListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
}

export interface IssueDto extends AuditedEntityDto<string> {
  companyId?: string;
  subject?: string;
  description?: string | null;
  status?: IssueStatus;
  priority?: string;
  issueType?: string | null;
  customerId?: string | null;
  assignedToId?: string | null;
  raisedVia?: string | null;
  openingDate?: string;
  resolutionDate?: string | null;
  resolution?: string | null;
  firstRespondedOn?: string | null;
  totalHoldTimeSeconds?: number | null;
  isSlaBreach?: boolean;
  serviceLevelAgreementId?: string | null;
  firstResponseTime?: number | null;
  resolutionTime?: number | null;
  responseByDate?: string | null;
  resolutionByDate?: string | null;
  agreementStatus?: AgreementStatus;
  splitFromIssueId?: string | null;
}

export interface IssuePriorityDto extends AuditedEntityDto<string> {
  name?: string;
  description?: string | null;
}

export interface IssueTypeDto extends AuditedEntityDto<string> {
  name?: string;
  description?: string | null;
}

export interface ResolveIssueDto {
  resolution?: string | null;
}

export interface SaveSupportSettingsDto {
  companyId?: string;
  trackServiceLevelAgreement?: boolean;
  allowResettingServiceLevelAgreement?: boolean;
  closeIssueAfterDays?: number | null;
}

export interface ServiceDayDto {
  id?: string;
  dayOfWeek?: any;
  startTime?: string;
  endTime?: string;
}

export interface ServiceLevelAgreementDto extends AuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  entityType?: string | null;
  entityId?: string | null;
  holidayListId?: string | null;
  resolutionTimeHours?: number;
  responseTimeHours?: number;
  isDefault?: boolean;
  applyOnResolution?: boolean;
  isActive?: boolean;
  priorities?: ServiceLevelPriorityDto[];
  serviceDays?: ServiceDayDto[];
}

export interface ServiceLevelPriorityDto {
  id?: string;
  priorityName?: string;
  responseTimeHours?: number;
  resolutionTimeHours?: number;
  isDefault?: boolean;
}

export interface SplitIssueDto {
  subject: string;
}

export interface SupportSettingsDto extends EntityDto<string> {
  companyId?: string;
  trackServiceLevelAgreement?: boolean;
  allowResettingServiceLevelAgreement?: boolean;
  closeIssueAfterDays?: number | null;
}
