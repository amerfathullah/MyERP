import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';

export interface CreatePayrollEntryDto {
  companyId: string;
  year: number;
  month: number;
}

export interface CreateUpdateEmployeeDto {
  companyId: string;
  firstName: string;
  lastName?: string | null;
  dateOfBirth?: string | null;
  dateOfJoining?: string | null;
  phone?: string | null;
  email?: string | null;
  designation?: string | null;
  department?: string | null;
  epfNumber?: string | null;
  socsoNumber?: string | null;
  taxNumber?: string | null;
}

export interface EmployeeDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  employeeId?: string;
  firstName?: string;
  lastName?: string | null;
  fullName?: string | null;
  dateOfBirth?: string | null;
  dateOfJoining?: string | null;
  dateOfResignation?: string | null;
  citizenship?: string | null;
  phone?: string | null;
  email?: string | null;
  designation?: string | null;
  department?: string | null;
  status?: string | null;
}

export interface PayrollEntryDto extends EntityDto<string> {
  companyId?: string;
  payrollNumber?: string;
  year?: number;
  month?: number;
  periodLabel?: string;
  postingDate?: string;
  totalGrossSalary?: number;
  totalDeductions?: number;
  totalNetSalary?: number;
  totalEmployerContributions?: number;
  status?: string;
  lines?: PayrollEntryLineDto[];
}

export interface PayrollEntryLineDto {
  id?: string;
  employeeId?: string;
  employeeName?: string;
  grossSalary?: number;
  epfEmployee?: number;
  epfEmployer?: number;
  socsoEmployee?: number;
  socsoEmployer?: number;
  eisEmployee?: number;
  eisEmployer?: number;
  pcb?: number;
  totalDeductions?: number;
  netSalary?: number;
}


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

export interface CreateExpenseClaimDto { [key: string]: any; }
