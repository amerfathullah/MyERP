import type { AuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IssueStatus } from './issue-status.enum';

export interface CreateIssueDto {
  companyId: string;
  subject: string;
  description?: string | null;
  priority?: string | null;
  issueType?: string | null;
  customerId?: string | null;
  raisedVia?: string | null;
}

export interface GetIssueListDto extends PagedAndSortedResultRequestDto {
  status?: IssueStatus | null;
  companyId?: string | null;
  filter?: string | null;
  priority?: string | null;
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
}

export interface ResolveIssueDto {
  resolution?: string | null;
}
