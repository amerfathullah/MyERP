import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CompanyFilteredPagedRequestDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
  status?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface BulkOperationError {
  id?: string;
  message?: string;
}

export interface BulkOperationResultDto {
  succeeded?: number;
  failed?: number;
  total?: number;
  errors?: BulkOperationError[];
}
