import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface BranchDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  name?: string;
  code?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  isActive?: boolean;
  isHeadquarters?: boolean;
}

export interface CompanyDto extends FullAuditedEntityDto<string> {
  name?: string;
  shortName?: string | null;
  taxId?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  msicCode?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  currencyCode?: string;
  fiscalYearStartMonth?: number;
  isActive?: boolean;
}

export interface CreateUpdateBranchDto {
  companyId: string;
  name: string;
  code?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  isActive?: boolean;
  isHeadquarters?: boolean;
}

export interface CreateUpdateCompanyDto {
  name: string;
  shortName?: string | null;
  taxId?: string | null;
  registrationNumber?: string | null;
  sstRegistrationNumber?: string | null;
  msicCode?: string | null;
  phone?: string | null;
  email?: string | null;
  website?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  currencyCode: string;
  fiscalYearStartMonth?: number;
  isActive?: boolean;
}
