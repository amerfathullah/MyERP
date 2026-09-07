import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { BulkTransactionStatus } from './bulk-transaction-status.enum';

export interface BulkTransactionLogDetailDto extends FullAuditedEntityDto<string> {
  bulkTransactionLogId?: string;
  transactionName?: string;
  fromDocType?: string;
  toDocType?: string;
  status?: BulkTransactionStatus;
  errorDescription?: string | null;
  executedTime?: string | null;
  retriedCount?: number;
}

export interface BulkTransactionLogDto extends FullAuditedEntityDto<string> {
  title?: string;
  batchDate?: string;
  totalEntries?: number;
  succeededCount?: number;
  failedCount?: number;
  details?: BulkTransactionLogDetailDto[];
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

export interface GetBulkTransactionLogListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface RecordBulkTransactionResultDto {
  isSuccess?: boolean;
  errorDescription?: string | null;
}
