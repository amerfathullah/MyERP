import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';
import type { AccountType } from './account-type.enum';
import type { AccountSubType } from './account-sub-type.enum';
import type { PaymentType } from './payment-type.enum';

export interface AccountDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  accountCode?: string;
  accountName?: string;
  accountType?: AccountType;
  accountSubType?: AccountSubType | null;
  parentAccountId?: string | null;
  isGroup?: boolean;
  currency?: string | null;
  description?: string | null;
  isFrozen?: boolean;
  isActive?: boolean;
}

export interface CreatePaymentEntryDto {
  companyId: string;
  paymentType: PaymentType;
  postingDate: string;
  paidAmount: number;
  paidFromAccountId: string;
  paidToAccountId: string;
  modeOfPayment?: string | null;
  partyType?: string | null;
  partyId?: string | null;
  referenceNumber?: string | null;
  notes?: string | null;
  againstInvoiceId?: string | null;
  againstInvoiceType?: string | null;
}

export interface CreateUpdateAccountDto {
  companyId: string;
  accountCode: string;
  accountName: string;
  accountType: AccountType;
  accountSubType?: AccountSubType | null;
  parentAccountId?: string | null;
  isGroup?: boolean;
  currency?: string | null;
  description?: string | null;
  isFrozen?: boolean;
  isActive?: boolean;
}

export interface PaymentEntryDto extends EntityDto<string> {
  companyId?: string;
  paymentNumber?: string | null;
  paymentType?: string;
  postingDate?: string;
  modeOfPayment?: string | null;
  paidAmount?: number;
  currencyCode?: string;
  status?: string;
  referenceNumber?: string | null;
}
