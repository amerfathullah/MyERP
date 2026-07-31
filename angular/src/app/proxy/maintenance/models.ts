import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateWarrantyClaimDto {
  companyId: string;
  customerId: string;
  itemId: string;
  serialNoId?: string | null;
  salesInvoiceId?: string | null;
  warrantyExpiryDate?: string | null;
  amcExpiryDate?: string | null;
  complaintDate?: string;
  complaint?: string | null;
}

export interface GetWarrantyClaimListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  companyId?: string | null;
  status?: number | null;
}

export interface WarrantyClaimDto extends EntityDto<string> {
  companyId?: string;
  claimNumber?: string;
  customerId?: string;
  customerName?: string | null;
  itemId?: string;
  itemName?: string | null;
  serialNoId?: string | null;
  salesInvoiceId?: string | null;
  warrantyExpiryDate?: string | null;
  amcExpiryDate?: string | null;
  complaintDate?: string;
  complaint?: string | null;
  resolution?: string | null;
  resolutionDate?: string | null;
  status?: number;
  isUnderWarranty?: boolean;
}
