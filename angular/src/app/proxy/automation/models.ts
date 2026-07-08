export interface AutomationRuleDto {
  id?: string;
  name?: string;
  description?: string;
  trigger?: number;
  documentType?: string;
  conditionExpression?: string;
  action?: number;
  actionConfig?: string;
  companyId?: string;
  isActive?: boolean;
  priority?: number;
}

export interface CreateAutomationRuleDto {
  name: string;
  description?: string;
  trigger: number;
  documentType?: string;
  conditionExpression?: string;
  action: number;
  actionConfig?: string;
  companyId?: string;
  isActive?: boolean;
  priority?: number;
}

export interface UpdateAutomationRuleDto {
  name: string;
  description?: string;
  conditionExpression?: string;
  action: number;
  actionConfig?: string;
  companyId?: string;
  isActive?: boolean;
  priority?: number;
}

export interface AutomationExecutionLogDto {
  id?: string;
  automationRuleId?: string;
  ruleName?: string;
  sourceDocumentId?: string;
  sourceDocumentType?: string;
  isSuccess?: boolean;
  errorMessage?: string;
  executionDurationMs?: number;
  creationTime?: string;
}
