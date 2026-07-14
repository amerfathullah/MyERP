import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CompanyFilteredPagedRequestDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  filter?: string | null;
  status?: string | null;
}
