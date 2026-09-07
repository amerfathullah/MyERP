import type { EntityDto } from '@abp/ng.core';
import type { AutomationTrigger } from '../automation-trigger.enum';
import type { AutomationAction } from '../automation-action.enum';

export interface AutomationExecutionLogDto extends EntityDto<string> {
  automationRuleId?: string;
  ruleName?: string | null;
  sourceDocumentId?: string | null;
  sourceDocumentType?: string | null;
  isSuccess?: boolean;
  errorMessage?: string | null;
  executionDurationMs?: number;
  creationTime?: string;
}

export interface AutomationRuleDto extends EntityDto<string> {
  name?: string;
  description?: string | null;
  trigger?: AutomationTrigger;
  documentType?: string | null;
  conditionExpression?: string | null;
  action?: AutomationAction;
  actionConfig?: string | null;
  companyId?: string | null;
  isActive?: boolean;
  priority?: number;
}

export interface CreateAutomationRuleDto {
  name: string;
  description?: string | null;
  trigger: AutomationTrigger;
  documentType?: string | null;
  conditionExpression?: string | null;
  action: AutomationAction;
  actionConfig?: string | null;
  companyId?: string | null;
  isActive?: boolean;
  priority?: number;
}

export interface UpdateAutomationRuleDto {
  name: string;
  description?: string | null;
  conditionExpression?: string | null;
  action: AutomationAction;
  actionConfig?: string | null;
  companyId?: string | null;
  isActive?: boolean;
  priority?: number;
}
