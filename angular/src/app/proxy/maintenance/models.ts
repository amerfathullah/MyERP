import type { MaintenancePeriodicity } from './maintenance-periodicity.enum';
import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateMaintenanceScheduleDto {
  companyId: string;
  customerId: string;
  salesOrderId?: string | null;
  contactId?: string | null;
  addressId?: string | null;
  items: CreateMaintenanceScheduleItemDto[];
}

export interface CreateMaintenanceScheduleItemDto {
  itemId: string;
  serialNoId?: string | null;
  salesPersonId?: string | null;
  startDate: string;
  endDate: string;
  noOfVisits?: number;
  periodicity?: MaintenancePeriodicity;
}

export interface CreateMaintenanceVisitDto {
  companyId: string;
  customerId: string;
  contactId?: string | null;
  addressId?: string | null;
  visitDate: string;
  maintenanceType?: number;
  maintenanceScheduleId?: string | null;
  maintenanceScheduleDetailId?: string | null;
  warrantyClaimId?: string | null;
  purposes: CreateMaintenanceVisitPurposeDto[];
}

export interface CreateMaintenanceVisitPurposeDto {
  itemId: string;
  serialNoId?: string | null;
  servicePersonId?: string | null;
  workDone?: string | null;
  status?: number;
}

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

export interface GetMaintenanceScheduleListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  customerId?: string | null;
  status?: number | null;
}

export interface GetMaintenanceVisitListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  customerId?: string | null;
  maintenanceScheduleId?: string | null;
  maintenanceType?: number | null;
}

export interface GetWarrantyClaimListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  companyId?: string | null;
  status?: number | null;
}

export interface MaintenanceScheduleDetailDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string | null;
  scheduledDate?: string;
  actualDate?: string | null;
  salesPersonId?: string | null;
  salesPersonName?: string | null;
  status?: number;
}

export interface MaintenanceScheduleDto extends EntityDto<string> {
  companyId?: string;
  scheduleNumber?: string;
  customerId?: string;
  customerName?: string | null;
  salesOrderId?: string | null;
  salesOrderNumber?: string | null;
  status?: number;
  items?: MaintenanceScheduleItemDto[];
  scheduleDetails?: MaintenanceScheduleDetailDto[];
}

export interface MaintenanceScheduleItemDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string | null;
  serialNoId?: string | null;
  salesPersonId?: string | null;
  salesPersonName?: string | null;
  startDate?: string;
  endDate?: string;
  noOfVisits?: number;
  periodicity?: number;
}

export interface MaintenanceVisitDto extends EntityDto<string> {
  companyId?: string;
  visitNumber?: string;
  customerId?: string;
  customerName?: string | null;
  maintenanceType?: number;
  visitDate?: string;
  completionStatus?: number;
  maintenanceScheduleId?: string | null;
  maintenanceScheduleNumber?: string | null;
  warrantyClaimId?: string | null;
  warrantyClaimNumber?: string | null;
  isSubmitted?: boolean;
  isCancelled?: boolean;
  purposes?: MaintenanceVisitPurposeDto[];
}

export interface MaintenanceVisitPurposeDto extends EntityDto<string> {
  itemId?: string;
  itemName?: string | null;
  serialNoId?: string | null;
  workDone?: string | null;
  status?: number;
  servicePersonId?: string | null;
  servicePersonName?: string | null;
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
