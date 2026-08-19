import type { ExpiredAppointmentAction } from './expired-appointment-action.enum';
import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AppointmentStatus } from './appointment-status.enum';
import type { ContractStatus } from './contract-status.enum';
import type { ContractFulfilmentStatus } from './contract-fulfilment-status.enum';
import type { OpportunityType } from './opportunity-type.enum';
import type { EmailCampaignFor } from './email-campaign-for.enum';
import type { LeadSource } from './lead-source.enum';
import type { EmailCampaignStatus } from './email-campaign-status.enum';
import type { LeadStatus } from './lead-status.enum';
import type { OpportunityStatus } from './opportunity-status.enum';
import type { ShipmentStatus } from './shipment-status.enum';

export interface AddCompetitorDetailDto {
  parentType: string;
  parentId: string;
  competitorId: string;
}

export interface AddCrmNoteDto {
  noteText: string;
}

export interface AppointmentAvailabilityDto {
  id?: string;
  dayOfWeek?: any;
  fromTime?: string;
  toTime?: string;
}

export interface AppointmentBookingSettingsDto {
  id?: string;
  companyId?: string;
  appointmentDurationMinutes?: number;
  enableScheduling?: boolean;
  enableAppointmentPortal?: boolean;
  holidayListId?: string | null;
  advanceBookingDays?: number;
  verificationLinkExpiryMinutes?: number;
  actionForExpiredUnverified?: ExpiredAppointmentAction;
  agentUserIds?: string[];
  numberOfAgents?: number;
  availabilityOfSlots?: AppointmentAvailabilityDto[];
}

export interface AppointmentDto extends AuditedEntityDto<string> {
  companyId?: string;
  customerName?: string;
  phone?: string | null;
  email?: string | null;
  details?: string | null;
  scheduledTime?: string;
  status?: AppointmentStatus;
  createdThroughPortal?: boolean;
  emailVerified?: boolean;
  partyType?: string | null;
  partyId?: string | null;
  assignedAgentUserId?: string | null;
  verificationToken?: string | null;
}

export interface CampaignDto extends AuditedEntityDto<string> {
  campaignName?: string;
  description?: string | null;
  emailSchedules?: CampaignEmailScheduleDto[];
}

export interface CampaignEmailScheduleDto {
  id?: string;
  emailTemplateId?: string;
  sendAfterDays?: number;
}

export interface CompetitorDetailDto {
  id?: string;
  parentType?: string;
  parentId?: string;
  competitorId?: string;
  competitorName?: string | null;
}

export interface CompetitorDto extends AuditedEntityDto<string> {
  name?: string;
  website?: string | null;
}

export interface ContractDto extends EntityDto<string> {
  companyId?: string;
  contractNumber?: string;
  contractName?: string | null;
  partyType?: string;
  partyId?: string;
  partyName?: string | null;
  startDate?: string;
  endDate?: string | null;
  signingDate?: string | null;
  status?: ContractStatus;
  contractValue?: number | null;
  currencyCode?: string | null;
  requiresFulfilment?: boolean;
  isAutoRenewal?: boolean;
  notes?: string | null;
  contractTemplateId?: string | null;
  contractTerms?: string | null;
  fulfilmentDeadline?: string | null;
  fulfilmentStatus?: ContractFulfilmentStatus;
  fulfilmentChecklist?: ContractFulfilmentChecklistItemDto[];
}

export interface ContractFulfilmentChecklistItemDto extends EntityDto<string> {
  requirement?: string;
  fulfilled?: boolean;
  notes?: string | null;
}

export interface ContractTemplateDto extends AuditedEntityDto<string> {
  title?: string;
  contractTerms?: string | null;
  requiresFulfilment?: boolean;
  fulfilmentTerms?: ContractTemplateFulfilmentTermDto[];
}

export interface ContractTemplateFulfilmentTermDto {
  id?: string;
  termText?: string;
}

export interface ConvertLeadToCustomerDto {
  leadId: string;
  customerName?: string | null;
  tin?: string | null;
  customerGroupId?: string | null;
  territoryId?: string | null;
}

export interface ConvertLeadToOpportunityDto {
  leadId: string;
  title: string;
  opportunityType?: OpportunityType;
  opportunityAmount?: number;
  salesStage?: string | null;
  expectedClosingDate?: string | null;
}

export interface CreateAppointmentDto {
  companyId: string;
  customerName: string;
  phone?: string | null;
  email?: string | null;
  details?: string | null;
  scheduledTime: string;
  createdThroughPortal?: boolean;
  partyType?: string | null;
  partyId?: string | null;
}

export interface CreateCampaignEmailScheduleDto {
  emailTemplateId: string;
  sendAfterDays?: number;
}

export interface CreateContractDto {
  companyId?: string;
  contractName?: string | null;
  partyType?: string;
  partyId?: string;
  startDate?: string;
  endDate?: string | null;
  contractTemplateId?: string | null;
  contractTerms?: string | null;
  contractValue?: number | null;
  currencyCode?: string | null;
  requiresFulfilment?: boolean;
  isAutoRenewal?: boolean;
  renewalReminderDays?: number | null;
  fulfilmentDeadline?: string | null;
  notes?: string | null;
}

export interface CreateEmailCampaignDto {
  campaignId: string;
  emailCampaignFor?: EmailCampaignFor;
  recipientId: string;
  senderId?: string | null;
  startDate?: string;
}

export interface CreateFulfilmentTermDto {
  termText: string;
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

export interface CreateProspectDto {
  companyId?: string;
  prospectName?: string;
  companyName?: string | null;
  industry?: string | null;
  website?: string | null;
  territory?: string | null;
  customerGroup?: string | null;
  annualRevenue?: number | null;
  numberOfEmployees?: number | null;
  notes?: string | null;
}

export interface CreateShipmentDto {
  companyId?: string;
  pickupFromType?: string | null;
  pickupFromId?: string | null;
  pickupAddressId?: string | null;
  deliveryToType?: string | null;
  deliveryToId?: string | null;
  deliveryAddressId?: string | null;
  pickupDate?: string | null;
  carrier?: string | null;
  carrierService?: string | null;
  totalNetWeight?: number | null;
  totalGrossWeight?: number | null;
  weightUom?: string | null;
  valueOfGoods?: number | null;
  currencyCode?: string | null;
  notes?: string | null;
  deliveryNoteIds?: string[] | null;
}

export interface CreateUpdateCampaignDto {
  campaignName: string;
  description?: string | null;
  emailSchedules?: CreateCampaignEmailScheduleDto[];
}

export interface CreateUpdateCompetitorDto {
  name: string;
  website?: string | null;
}

export interface CreateUpdateContractTemplateDto {
  title: string;
  contractTerms?: string | null;
  requiresFulfilment?: boolean;
  fulfilmentTerms?: CreateFulfilmentTermDto[];
}

export interface CreateUpdateIndustryTypeDto {
  name: string;
}

export interface CreateUpdateMarketSegmentDto {
  name: string;
}

export interface CreateUpdateSalesStageDto {
  stageName: string;
  sortOrder?: number;
}

export interface CrmNoteDto extends EntityDto<string> {
  parentType?: string;
  parentId?: string;
  noteText?: string;
  addedByUserId?: string;
  addedOn?: string;
}

export interface EmailCampaignDto extends AuditedEntityDto<string> {
  campaignId?: string;
  emailCampaignFor?: EmailCampaignFor;
  recipientId?: string;
  senderId?: string | null;
  startDate?: string;
  endDate?: string;
  status?: EmailCampaignStatus;
}

export interface GetAppointmentListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  status?: AppointmentStatus | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface GetCampaignListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetCompetitorListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetContractTemplateListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetEmailCampaignListDto extends PagedAndSortedResultRequestDto {
  campaignId?: string | null;
  status?: EmailCampaignStatus | null;
}

export interface GetIndustryTypeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetLeadListDto extends PagedAndSortedResultRequestDto {
  status?: LeadStatus | null;
  source?: LeadSource | null;
  filter?: string | null;
  companyId?: string | null;
}

export interface GetMarketSegmentListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetOpportunityListDto extends PagedAndSortedResultRequestDto {
  status?: OpportunityStatus | null;
  opportunityType?: OpportunityType | null;
  filter?: string | null;
  companyId?: string | null;
  leadId?: string | null;
}

export interface GetSalesStageListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface IndustryTypeDto extends AuditedEntityDto<string> {
  name?: string;
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

export interface MarketSegmentDto extends AuditedEntityDto<string> {
  name?: string;
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
  competitors?: CompetitorDetailDto[];
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

export interface PipelineOpportunityDto {
  id?: string;
  title?: string;
  salesStage?: string;
  amount?: number;
  probability?: number;
  weightedAmount?: number;
  expectedClosingDate?: string | null;
  contactName?: string | null;
  daysOpen?: number;
}

export interface PipelineStageDto {
  stageName?: string;
  count?: number;
  totalAmount?: number;
  weightedAmount?: number;
  avgProbability?: number;
}

export interface ProspectDto extends EntityDto<string> {
  companyId?: string;
  prospectName?: string;
  companyName?: string | null;
  industry?: string | null;
  website?: string | null;
  territory?: string | null;
  annualRevenue?: number | null;
  numberOfEmployees?: number | null;
  isConverted?: boolean;
  convertedCustomerId?: string | null;
  leadCount?: number;
  opportunityCount?: number;
  notes?: string | null;
}

export interface SalesPipelineDashboardDto {
  totalLeads?: number;
  activeLeads?: number;
  qualifiedLeads?: number;
  lostLeads?: number;
  totalOpportunities?: number;
  openOpportunities?: number;
  openOpportunitiesAmount?: number;
  weightedPipelineValue?: number;
  wonOpportunities?: number;
  wonAmount?: number;
  lostOpportunities?: number;
  stageBreakdown?: PipelineStageDto[];
  totalQuotations?: number;
  openQuotations?: number;
  openQuotationsAmount?: number;
  convertedQuotations?: number;
  ordersThisMonth?: number;
  ordersThisMonthAmount?: number;
  leadToOpportunityRate?: number;
  opportunityToQuotationRate?: number;
  quotationToOrderRate?: number;
}

export interface SalesStageDto extends AuditedEntityDto<string> {
  stageName?: string;
  sortOrder?: number;
}

export interface SaveAppointmentAvailabilityDto {
  dayOfWeek?: any;
  fromTime?: string;
  toTime?: string;
}

export interface SaveAppointmentBookingSettingsDto {
  companyId?: string;
  appointmentDurationMinutes?: number;
  enableScheduling?: boolean;
  enableAppointmentPortal?: boolean;
  holidayListId?: string | null;
  advanceBookingDays?: number;
  verificationLinkExpiryMinutes?: number;
  actionForExpiredUnverified?: ExpiredAppointmentAction;
  agentUserIds?: string[];
  availabilityOfSlots?: SaveAppointmentAvailabilityDto[];
}

export interface ShipmentDto extends EntityDto<string> {
  companyId?: string;
  shipmentNumber?: string;
  pickupFromName?: string | null;
  deliveryToName?: string | null;
  pickupDate?: string | null;
  deliveryDate?: string | null;
  carrier?: string | null;
  trackingNumber?: string | null;
  trackingUrl?: string | null;
  status?: ShipmentStatus;
  deliveryNoteCount?: number;
  totalNetWeight?: number | null;
  valueOfGoods?: number | null;
  currencyCode?: string | null;
  notes?: string | null;
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

export interface UpdateOpportunityStageDto {
  salesStage: string;
}

export interface VerifyAppointmentDto {
  token: string;
}
