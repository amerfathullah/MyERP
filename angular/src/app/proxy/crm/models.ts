export interface LeadDto {
  id?: string;
  leadNumber?: string;
  firstName?: string;
  lastName?: string;
  companyName?: string;
  email?: string;
  phone?: string;
  mobileNo?: string;
  jobTitle?: string;
  website?: string;
  status?: number;
  source?: number;
  city?: string;
  state?: string;
  country?: string;
  industry?: string;
  annualRevenue?: number;
  assignedUserId?: string;
  convertedCustomerId?: string;
  convertedOpportunityId?: string;
  companyId?: string;
  notes?: string;
  fullName?: string;
  creationTime?: string;
  lastModificationTime?: string;
}

export interface CreateLeadDto {
  firstName: string;
  lastName?: string;
  companyName?: string;
  email?: string;
  phone?: string;
  mobileNo?: string;
  jobTitle?: string;
  website?: string;
  source?: number;
  city?: string;
  state?: string;
  country?: string;
  industry?: string;
  annualRevenue?: number;
  assignedUserId?: string;
  companyId: string;
  notes?: string;
}

export interface UpdateLeadDto {
  firstName: string;
  lastName?: string;
  companyName?: string;
  email?: string;
  phone?: string;
  mobileNo?: string;
  jobTitle?: string;
  website?: string;
  source?: number;
  city?: string;
  state?: string;
  country?: string;
  industry?: string;
  annualRevenue?: number;
  assignedUserId?: string;
  notes?: string;
}

export interface GetLeadListDto {
  status?: number;
  source?: number;
  filter?: string;
  companyId?: string;
  skipCount?: number;
  maxResultCount?: number;
  sorting?: string;
}

export interface ConvertLeadToOpportunityDto {
  leadId: string;
  title: string;
  opportunityType?: number;
  opportunityAmount?: number;
  salesStage?: string;
  expectedClosingDate?: string;
}

export interface OpportunityDto {
  id?: string;
  opportunityNumber?: string;
  title?: string;
  status?: number;
  opportunityType?: number;
  leadId?: string;
  customerId?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  salesStage?: string;
  probability?: number;
  expectedClosingDate?: string;
  opportunityAmount?: number;
  currencyCode?: string;
  companyId?: string;
  assignedUserId?: string;
  territory?: string;
  lostReason?: string;
  notes?: string;
  items?: OpportunityItemDto[];
  creationTime?: string;
  lastModificationTime?: string;
}

export interface OpportunityItemDto {
  id?: string;
  itemId?: string;
  description?: string;
  quantity?: number;
  unitPrice?: number;
  amount?: number;
  uom?: string;
}

export interface CreateOpportunityDto {
  title: string;
  opportunityType?: number;
  leadId?: string;
  customerId?: string;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  salesStage?: string;
  probability?: number;
  expectedClosingDate?: string;
  opportunityAmount?: number;
  currencyCode?: string;
  companyId: string;
  assignedUserId?: string;
  territory?: string;
  notes?: string;
  items?: { itemId?: string; description: string; quantity: number; unitPrice: number; uom?: string }[];
}

export interface UpdateOpportunityDto {
  title: string;
  opportunityType?: number;
  contactName?: string;
  contactEmail?: string;
  contactPhone?: string;
  salesStage?: string;
  probability?: number;
  expectedClosingDate?: string;
  opportunityAmount?: number;
  currencyCode?: string;
  assignedUserId?: string;
  territory?: string;
  notes?: string;
  items?: { itemId?: string; description: string; quantity: number; unitPrice: number; uom?: string }[];
}

export interface GetOpportunityListDto { [key: string]: any; }
