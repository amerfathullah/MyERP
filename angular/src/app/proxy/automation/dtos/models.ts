import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AutomationTrigger } from '../automation-trigger.enum';
import type { AutomationAction } from '../automation-action.enum';
import type { BulkTransactionStatus } from '../bulk-transaction-status.enum';

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

export interface BulkTransactionLogDetailDto extends FullAuditedEntityDto<string> {
  bulkTransactionLogId: string;
  transactionName: string;
  fromDocType: string;
  toDocType: string;
  status: BulkTransactionStatus;
  errorDescription?: string | null;
  executedTime?: string | null;
  retriedCount: number;
}

export interface BulkTransactionLogDto extends FullAuditedEntityDto<string> {
  title: string;
  batchDate: string;
  totalEntries: number;
  succeededCount: number;
  failedCount: number;
  details: BulkTransactionLogDetailDto[];
}

export interface CreateBulkTransactionLogDetailDto {
  transactionName: string;
  fromDocType: string;
  toDocType: string;
}

export interface CreateBulkTransactionLogDto {
  title: string;
  batchDate?: string;
  details?: CreateBulkTransactionLogDetailDto[];
}

export interface RecordBulkTransactionResultDto {
  isSuccess: boolean;
  errorDescription?: string | null;
}

export interface GetBulkTransactionLogListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}
