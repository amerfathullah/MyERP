import type { TaxType } from './tax-type.enum';
import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateItemTaxTemplateDetailDto {
  taxAccountId?: string;
  taxRate?: number;
  notApplicable?: boolean;
}

export interface CreateItemTaxTemplateDto {
  companyId?: string;
  title?: string;
  details?: CreateItemTaxTemplateDetailDto[];
}

export interface CreateUpdateTaxCategoryDto {
  code: string;
  name: string;
  description?: string | null;
  taxType: TaxType;
  isActive?: boolean;
}

export interface CreateUpdateTaxRuleDto {
  taxCategoryId: string;
  rate: number;
  effectiveFrom: string;
  effectiveTo?: string | null;
  itemGroupFilter?: string | null;
  regionFilter?: string | null;
  priority?: number;
  description?: string | null;
  isActive?: boolean;
}

export interface ItemTaxTemplateDetailDto {
  id?: string;
  taxAccountId?: string;
  taxRate?: number;
  notApplicable?: boolean;
}

export interface ItemTaxTemplateDto extends EntityDto<string> {
  companyId?: string;
  title?: string;
  isDisabled?: boolean;
  details?: ItemTaxTemplateDetailDto[];
}

export interface Sst02FilingDataDto {
  companyId?: string;
  companyName?: string;
  sstRegistrationNumber?: string | null;
  taxPeriod?: string;
  fromDate?: string;
  toDate?: string;
  taxableSupplies6Percent?: number;
  taxableSupplies10Percent?: number;
  taxableSupplies5Percent?: number;
  taxableSuppliesOtherRate?: number;
  exemptSupplies?: number;
  zeroRatedSupplies?: number;
  outputTax6Percent?: number;
  outputTax10Percent?: number;
  outputTax5Percent?: number;
  outputTaxOther?: number;
  totalOutputTax?: number;
  inputTaxCredit?: number;
  creditNoteAdjustment?: number;
  debitNoteAdjustment?: number;
  badDebtRelief?: number;
  netAdjustment?: number;
  netTaxPayable?: number;
  isRefundable?: boolean;
  totalSalesInvoices?: number;
  totalPurchaseInvoices?: number;
  totalCreditNotes?: number;
  totalDebitNotes?: number;
}

export interface TaxCategoryDto extends FullAuditedEntityDto<string> {
  code?: string;
  name?: string;
  description?: string | null;
  taxType?: string;
  isActive?: boolean;
}

export interface TaxRateBreakdownDto {
  taxRate?: string;
  taxableAmount?: number;
  taxAmount?: number;
  invoiceCount?: number;
}

export interface TaxRuleDto extends EntityDto<string> {
  taxCategoryId?: string;
  rate?: number;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  itemGroupFilter?: string | null;
  regionFilter?: string | null;
  priority?: number;
  description?: string | null;
  isActive?: boolean;
}

export interface TaxSummaryDto {
  companyId?: string;
  fromDate?: string;
  toDate?: string;
  totalSalesAmount?: number;
  outputTax?: number;
  creditNoteTaxAdjustment?: number;
  netOutputTax?: number;
  salesInvoiceCount?: number;
  creditNoteCount?: number;
  totalPurchaseAmount?: number;
  inputTax?: number;
  debitNoteTaxAdjustment?: number;
  netInputTax?: number;
  purchaseInvoiceCount?: number;
  debitNoteCount?: number;
  netTaxPayable?: number;
  isRefundable?: boolean;
  outputTaxBreakdown?: TaxRateBreakdownDto[];
  inputTaxBreakdown?: TaxRateBreakdownDto[];
}
