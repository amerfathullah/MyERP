import type { OpportunityType } from './opportunity-type.enum';
import type { LeadSource } from './lead-source.enum';
import type { AuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { LeadStatus } from './lead-status.enum';
import type { OpportunityStatus } from './opportunity-status.enum';

export interface ConvertLeadToOpportunityDto {
  leadId: string;
  title: string;
  opportunityType?: OpportunityType;
  opportunityAmount?: number;
  salesStage?: string | null;
  expectedClosingDate?: string | null;
}

export interface CreateLeadDto {
  firstName: string;
  lastName?: string | null;
  companyName?: string | null;
  email?: string | null;
  phone?: string | null;
  mobileNo?: string | null;
  jobTitle?: string | null;
  website?: string | null;
  source?: LeadSource;
  city?: string | null;
  state?: string | null;
  country?: string | null;
  industry?: string | null;
  annualRevenue?: number | null;
  assignedUserId?: string | null;
  companyId: string;
  notes?: string | null;
}

export interface CreateOpportunityDto {
  title: string;
  opportunityType?: OpportunityType;
  leadId?: string | null;
  customerId?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  salesStage?: string | null;
  probability?: number;
  expectedClosingDate?: string | null;
  opportunityAmount?: number;
  currencyCode?: string;
  companyId: string;
  assignedUserId?: string | null;
  territory?: string | null;
  notes?: string | null;
  items?: CreateOpportunityItemDto[];
}

export interface CreateOpportunityItemDto {
  itemId?: string | null;
  description: string;
  quantity?: number;
  unitPrice?: number;
  uom?: string | null;
}

export interface GetLeadListDto extends PagedAndSortedResultRequestDto {
  status?: LeadStatus | null;
  source?: LeadSource | null;
  filter?: string | null;
  companyId?: string | null;
}

export interface GetOpportunityListDto extends PagedAndSortedResultRequestDto {
  status?: OpportunityStatus | null;
  opportunityType?: OpportunityType | null;
  filter?: string | null;
  companyId?: string | null;
  leadId?: string | null;
}

export interface LeadDto extends AuditedEntityDto<string> {
  leadNumber?: string;
  firstName?: string;
  lastName?: string | null;
  companyName?: string | null;
  email?: string | null;
  phone?: string | null;
  mobileNo?: string | null;
  jobTitle?: string | null;
  website?: string | null;
  status?: LeadStatus;
  source?: LeadSource;
  city?: string | null;
  state?: string | null;
  country?: string | null;
  industry?: string | null;
  annualRevenue?: number | null;
  assignedUserId?: string | null;
  convertedCustomerId?: string | null;
  convertedOpportunityId?: string | null;
  companyId?: string;
  notes?: string | null;
  fullName?: string | null;
}

export interface OpportunityDto extends AuditedEntityDto<string> {
  opportunityNumber?: string;
  title?: string;
  status?: OpportunityStatus;
  opportunityType?: OpportunityType;
  leadId?: string | null;
  customerId?: string | null;
  contactName?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  salesStage?: string | null;
  probability?: number;
  expectedClosingDate?: string | null;
  opportunityAmount?: number;
  currencyCode?: string;
  companyId?: string;
  assignedUserId?: string | null;
  territory?: string | null;
  lostReason?: string | null;
  notes?: string | null;
  items?: OpportunityItemDto[];
}

export interface OpportunityItemDto {
  id?: string;
  itemId?: string | null;
  description?: string;
  quantity?: number;
  unitPrice?: number;
  amount?: number;
  uom?: string | null;
}

export interface UpdateLeadDto {
  firstName: string;
  lastName?: string | null;
  companyName?: string | null;
  email?: string | null;
  phone?: string | null;
  mobileNo?: string | null;
  jobTitle?: string | null;
  website?: string | null;
  source?: LeadSource;
  city?: string | null;
  state?: string | null;
  country?: string | null;
  industry?: string | null;
  annualRevenue?: number | null;
  assignedUserId?: string | null;
  notes?: string | null;
}

export interface UpdateOpportunityDto {
  title: string;
  opportunityType?: OpportunityType;
  contactName?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  salesStage?: string | null;
  probability?: number;
  expectedClosingDate?: string | null;
  opportunityAmount?: number;
  currencyCode?: string;
  assignedUserId?: string | null;
  territory?: string | null;
  notes?: string | null;
  items?: CreateOpportunityItemDto[];
}
