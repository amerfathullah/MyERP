import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface EmployeeDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  firstName?: string;
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
  status?: string;
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

export interface PayrollEntryDto extends FullAuditedEntityDto<string> {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  payrollNumber?: string | null;
  period?: string;
  periodLabel?: string | null;
  basicSalary?: number;
  grossPay?: number;
  totalGrossSalary?: number;
  totalDeductions?: number;
  totalNetSalary?: number;
  totalEmployerContributions?: number;
  netPay?: number;
  epfEmployee?: number;
  epfEmployer?: number;
  socsoEmployee?: number;
  socsoEmployer?: number;
  eisEmployee?: number;
  eisEmployer?: number;
  mtdAmount?: number;
  status?: string;
  lines?: PayrollLineDto[];
}

export interface PayrollLineDto {
  employeeId?: string;
  employeeName?: string | null;
  grossSalary?: number;
  epfEmployee?: number;
  socsoEmployee?: number;
  eisEmployee?: number;
  mtdAmount?: number;
  totalDeductions?: number;
  netSalary?: number;
  epfEmployer?: number;
  socsoEmployer?: number;
  eisEmployer?: number;
}

export interface CreatePayrollEntryDto {
  companyId: string;
  employeeId: string;
  period: string;
  basicSalary: number;
}
