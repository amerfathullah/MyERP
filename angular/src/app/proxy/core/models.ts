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

export interface DashboardSummaryDto {
  totalCustomers?: number;
  totalSuppliers?: number;
  totalItems?: number;
  draftInvoices?: number;
  outstandingInvoices?: number;
  pendingPurchaseOrders?: number;
  submittedEInvoices?: number;
  pendingApprovals?: number;
  monthlyRevenue?: number;
  monthlyExpenses?: number;
}

export interface CreateDocumentSeriesDto { [key: string]: any; }

export interface DocumentSeriesDto { [key: string]: any; }


export interface CreateContactDto { [key: string]: any; }

export interface DocumentActivityLogDto { [key: string]: any; }

export interface CreateExpenseClaimDto { [key: string]: any; }

export interface ExpenseClaimDto { [key: string]: any; }

export interface CreateHolidayListDto { [key: string]: any; }

export interface HolidayListDto { [key: string]: any; }

export interface BulkLeaveAllocationDto { [key: string]: any; }

export interface CreateLeaveAllocationDto { [key: string]: any; }

export interface GetLeaveAllocationListDto { [key: string]: any; }

export interface LeaveAllocationDto { [key: string]: any; }

export interface CreateLeaveApplicationDto { [key: string]: any; }

export interface CreateLeaveTypeDto { [key: string]: any; }

export interface GetLeaveListDto { [key: string]: any; }

export interface LeaveApplicationDto { [key: string]: any; }

export interface LeaveTypeDto { [key: string]: any; }

export interface SalarySlipDto { [key: string]: any; }

export interface CreateSalaryStructureDto { [key: string]: any; }

export interface SalaryStructureDto { [key: string]: any; }

export interface GetBatchListDto { [key: string]: any; }

export interface CreateItemGroupDto { [key: string]: any; }

export interface ItemGroupDto { [key: string]: any; }

export interface CreatePickListDto { [key: string]: any; }

export interface PickListDto { [key: string]: any; }

export interface GetItemPriceListDto { [key: string]: any; }

export interface GetItemRateRequestDto { [key: string]: any; }

export interface ItemPriceDto { [key: string]: any; }

export interface ItemRateResultDto { [key: string]: any; }

export interface PriceListDto { [key: string]: any; }

export interface GetSerialNoListDto { [key: string]: any; }

export interface GetJobCardListDto { [key: string]: any; }

export interface CreateOperationDto { [key: string]: any; }

export interface OperationDto { [key: string]: any; }

export interface CreateRoutingDto { [key: string]: any; }

export interface RoutingDto { [key: string]: any; }

export interface PurchaseRegisterLineDto { [key: string]: any; }

export interface RegisterFilterDto { [key: string]: any; }

export interface RegisterReportDto { [key: string]: any; }

export interface CreateSubcontractingReceiptDto { [key: string]: any; }

export interface GetScoListDto { [key: string]: any; }

export interface CustomerRevenueReportDto { [key: string]: any; }

export interface GrossProfitReportDto { [key: string]: any; }

export interface GrossProfitRequestDto { [key: string]: any; }

export interface ItemSalesReportDto { [key: string]: any; }

export interface CreateProductBundleDto { [key: string]: any; }

export interface ProductBundleDto { [key: string]: any; }

export interface SalesRegisterLineDto { [key: string]: any; }

export interface GeneratedInvoiceDto { [key: string]: any; }

export interface ContactDto { [key: string]: any; }
