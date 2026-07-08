import type { EntityDto } from '@abp/ng.core';
import type { ApprovalStatus } from '../approval-status.enum';

export interface ApprovalRequestDto extends EntityDto<string> {
  approvalRuleId?: string;
  documentType?: string;
  documentId?: string;
  level?: number;
  status?: ApprovalStatus;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  remarks?: string | null;
  requestedByUserId?: string;
  creationTime?: string;
  ruleName?: string | null;
}

export interface ApprovalRuleDto extends EntityDto<string> {
  documentType?: string;
  name?: string;
  level?: number;
  approverRoleName?: string | null;
  approverUserId?: string | null;
  conditionExpression?: string | null;
  minimumAmount?: number | null;
  companyId?: string | null;
  isActive?: boolean;
  description?: string | null;
}

export interface CreateApprovalRuleDto {
  documentType: string;
  name: string;
  level?: number;
  approverRoleName?: string | null;
  approverUserId?: string | null;
  conditionExpression?: string | null;
  minimumAmount?: number | null;
  companyId?: string | null;
  isActive?: boolean;
  description?: string | null;
}

export interface ReviewApprovalDto {
  requestId: string;
  remarks?: string | null;
}

export interface UpdateApprovalRuleDto {
  name: string;
  level?: number;
  approverRoleName?: string | null;
  approverUserId?: string | null;
  conditionExpression?: string | null;
  minimumAmount?: number | null;
  companyId?: string | null;
  isActive?: boolean;
  description?: string | null;
}
