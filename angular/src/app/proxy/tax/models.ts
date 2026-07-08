import type { FullAuditedEntityDto, EntityDto } from '@abp/ng.core';

export interface TaxCategoryDto extends FullAuditedEntityDto<string> {
  code?: string;
  name?: string;
  description?: string | null;
  taxType?: string;
  isActive?: boolean;
}

export interface CreateUpdateTaxCategoryDto {
  code: string;
  name: string;
  description?: string | null;
  taxType: string;
  isActive?: boolean;
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
