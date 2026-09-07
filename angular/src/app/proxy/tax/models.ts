import type { TaxTemplateType } from './tax-template-type.enum';
import type { TaxDeductionBasis } from './tax-deduction-basis.enum';
import type { TaxType } from './tax-type.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

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

export interface CreateTaxChargesTemplateDto {
  companyId?: string;
  name?: string;
  templateType?: TaxTemplateType;
  taxCategoryId?: string | null;
  isDefault?: boolean;
  rows?: CreateTaxChargesTemplateRowDto[];
}

export interface CreateTaxChargesTemplateRowDto {
  chargeType?: string;
  rate?: number;
  accountId?: string | null;
  accountName?: string | null;
  taxCategory?: string | null;
  referenceRowIndex?: number | null;
  includedInPrintRate?: boolean;
  description?: string | null;
  costCenterId?: string | null;
}

export interface CreateTaxWithholdingAccountDto {
  companyId: string;
  accountId: string;
}

export interface CreateTaxWithholdingCategoryDto {
  categoryName: string;
  taxDeductionBasis: TaxDeductionBasis;
  roundOffTaxAmount?: boolean;
  taxOnExcessAmount?: boolean;
  disableCumulativeThreshold?: boolean;
  disableTransactionThreshold?: boolean;
  rates?: CreateTaxWithholdingRateDto[];
  accounts?: CreateTaxWithholdingAccountDto[];
}

export interface CreateTaxWithholdingRateDto {
  fromDate: string;
  toDate: string;
  rate: number;
  singleThreshold?: number | null;
  cumulativeThreshold?: number | null;
  group?: string | null;
}

export interface CreateUpdateLowerDeductionCertificateDto {
  companyId: string;
  supplierId: string;
  taxWithholdingCategoryId: string;
  certificateNumber: string;
  rate?: number;
  certificateLimit?: number;
  validFrom: string;
  validUpto: string;
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

export interface CreateUpdateTaxWithholdingGroupDto {
  groupName: string;
  description?: string | null;
  isActive?: boolean;
}

export interface DefaultTaxLineDto {
  taxName?: string;
  rate?: number;
  chargeType?: string;
  accountId?: string | null;
  taxCategoryCode?: string | null;
}

export interface GetLowerDeductionCertificateListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  supplierId?: string | null;
}

export interface GetTaxTemplateListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  templateType?: TaxTemplateType | null;
  filter?: string | null;
}

export interface GetTaxWithholdingGroupListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  isActive?: boolean | null;
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

export interface LowerDeductionCertificateDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  supplierId?: string;
  supplierName?: string | null;
  taxWithholdingCategoryId?: string;
  taxWithholdingCategoryName?: string | null;
  certificateNumber?: string;
  rate?: number;
  certificateLimit?: number;
  validFrom?: string;
  validUpto?: string;
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

export interface TaxChargesTemplateDto extends EntityDto<string> {
  companyId?: string;
  name?: string;
  templateType?: TaxTemplateType;
  taxCategoryId?: string | null;
  isDefault?: boolean;
  isEnabled?: boolean;
  rows?: TaxChargesTemplateRowDto[];
}

export interface TaxChargesTemplateRowDto {
  id?: string;
  rowIndex?: number;
  chargeType?: string;
  rate?: number;
  accountId?: string | null;
  accountName?: string | null;
  taxCategory?: string;
  referenceRowIndex?: number | null;
  includedInPrintRate?: boolean;
  description?: string | null;
  costCenterId?: string | null;
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

export interface TaxWithholdingAccountDto {
  id?: string;
  companyId?: string;
  accountId?: string;
}

export interface TaxWithholdingCategoryDto extends FullAuditedEntityDto<string> {
  categoryName?: string;
  taxDeductionBasis?: string;
  roundOffTaxAmount?: boolean;
  taxOnExcessAmount?: boolean;
  disableCumulativeThreshold?: boolean;
  disableTransactionThreshold?: boolean;
  rates?: TaxWithholdingRateDto[];
  accounts?: TaxWithholdingAccountDto[];
}

export interface TaxWithholdingGroupDto extends FullAuditedEntityDto<string> {
  groupName?: string;
  description?: string | null;
  isActive?: boolean;
}

export interface TaxWithholdingRateDto {
  id?: string;
  fromDate?: string;
  toDate?: string;
  rate?: number;
  singleThreshold?: number | null;
  cumulativeThreshold?: number | null;
  group?: string | null;
}

export interface UpdateItemTaxTemplateDto {
  title?: string;
  isDisabled?: boolean;
  details?: CreateItemTaxTemplateDetailDto[];
}

export interface UpdateTaxWithholdingCategoryDto extends CreateTaxWithholdingCategoryDto {
}
