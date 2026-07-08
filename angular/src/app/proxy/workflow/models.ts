export interface ApprovalRuleDto {
  id?: string;
  documentType?: string;
  name?: string;
  level?: number;
  approverRoleName?: string;
  approverUserId?: string;
  conditionExpression?: string;
  minimumAmount?: number;
  companyId?: string;
  isActive?: boolean;
  description?: string;
}

export interface CreateApprovalRuleDto {
  documentType: string;
  name: string;
  level?: number;
  approverRoleName?: string;
  approverUserId?: string;
  conditionExpression?: string;
  minimumAmount?: number;
  companyId?: string;
  isActive?: boolean;
  description?: string;
}

export interface UpdateApprovalRuleDto {
  name: string;
  level?: number;
  approverRoleName?: string;
  approverUserId?: string;
  conditionExpression?: string;
  minimumAmount?: number;
  companyId?: string;
  isActive?: boolean;
  description?: string;
}

export interface ApprovalRequestDto {
  id?: string;
  approvalRuleId?: string;
  documentType?: string;
  documentId?: string;
  level?: number;
  status?: number;
  reviewedByUserId?: string;
  reviewedAt?: string;
  remarks?: string;
  requestedByUserId?: string;
  creationTime?: string;
  ruleName?: string;
}

export interface ReviewApprovalDto {
  requestId: string;
  remarks?: string;
}
