import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { LeaveApplicationStatus } from './entities/leave-application-status.enum';

export interface BulkLeaveAllocationDto {
  companyId: string;
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  totalLeavesPerEmployee: number;
}

export interface CreateExpenseClaimDto {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  postingDate?: string;
  expenseType?: string | null;
  expenses?: CreateExpenseDetailDto[];
}

export interface CreateExpenseDetailDto {
  expenseDate?: string;
  description?: string;
  amount?: number;
}

export interface CreateHolidayDto {
  holidayDate?: string;
  description?: string;
  isWeeklyOff?: boolean;
}

export interface CreateHolidayListDto {
  companyId?: string;
  name?: string;
  year?: number;
  weeklyOff?: string | null;
  isDefault?: boolean;
  holidays?: CreateHolidayDto[];
}

export interface CreateLeaveAllocationDto {
  companyId: string;
  employeeId: string;
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  totalLeavesAllocated: number;
  carryForwardDays?: number;
}

export interface CreateLeaveApplicationDto {
  companyId: string;
  employeeId: string;
  employeeName?: string | null;
  leaveTypeId: string;
  leaveTypeName?: string | null;
  fromDate: string;
  toDate: string;
  totalLeaveDays: number;
  halfDay?: boolean;
  reason?: string | null;
  leaveApproverId?: string | null;
}

export interface CreateLeaveTypeDto {
  name: string;
  maxDaysAllowed: number;
  isPaidLeave?: boolean;
  requiresApproval?: boolean;
  allowCarryForward?: boolean;
  maxCarryForwardDays?: number;
}

export interface CreateLoanDto {
  companyId?: string;
  employeeId?: string;
  loanType?: number;
  interestMethod?: number;
  loanAmount?: number;
  annualInterestRate?: number;
  tenureMonths?: number;
  gracePeriodMonths?: number;
}

export interface CreatePayrollEntryDto {
  companyId: string;
  year: number;
  month: number;
}

export interface CreateSalaryStructureDetailDto {
  salaryComponentId?: string;
  componentName?: string;
  amount?: number;
  formula?: string | null;
}

export interface CreateSalaryStructureDto {
  companyId?: string;
  name?: string;
  isHourlyBased?: boolean;
  payrollFrequency?: string;
  description?: string | null;
  details?: CreateSalaryStructureDetailDto[];
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

export interface CreateUpdateLeaveTypeDto {
  name?: string;
  maxDaysAllowed?: number;
  requiresApproval?: boolean;
  allowCarryForward?: boolean;
  maxCarryForwardDays?: number;
  carryForwardExpiryMonths?: number;
  isPaidLeave?: boolean;
  includeHolidays?: boolean;
  allowNegativeBalance?: boolean;
}

export interface CreateUpdateSalaryComponentDto {
  name?: string;
  abbreviation?: string | null;
  componentType?: number;
  isStatutory?: boolean;
  isTaxApplicable?: boolean;
  dependsOnPaymentDays?: boolean;
  defaultAccountId?: string | null;
  description?: string | null;
}

export interface DisburseLoanDto {
  disbursementDate?: string;
  repaymentStartDate?: string;
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

export interface ExpenseClaimDetailDto {
  id?: string;
  expenseDate?: string;
  description?: string;
  amount?: number;
}

export interface ExpenseClaimDto extends EntityDto<string> {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  postingDate?: string;
  expenseType?: string | null;
  totalClaimedAmount?: number;
  totalSanctionedAmount?: number;
  totalAmountReimbursed?: number;
  status?: number;
  expenses?: ExpenseClaimDetailDto[];
}

export interface GetEmployeeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  companyId?: string | null;
}

export interface GetLeaveAllocationListDto extends PagedAndSortedResultRequestDto {
  employeeId?: string | null;
  companyId?: string | null;
  leaveTypeId?: string | null;
}

export interface GetLeaveListDto extends PagedAndSortedResultRequestDto {
  employeeId?: string | null;
  status?: LeaveApplicationStatus | null;
}

export interface GetPayrollListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
}

export interface HolidayDto {
  id?: string;
  holidayDate?: string;
  description?: string;
  isWeeklyOff?: boolean;
}

export interface HolidayListDto extends EntityDto<string> {
  companyId?: string;
  name?: string;
  year?: number;
  weeklyOff?: string | null;
  isDefault?: boolean;
  holidays?: HolidayDto[];
  creationTime?: string;
}

export interface LeaveAllocationDto {
  id?: string;
  companyId?: string;
  employeeId?: string;
  leaveTypeId?: string;
  fromDate?: string;
  toDate?: string;
  totalLeavesAllocated?: number;
  carryForwardDays?: number;
  leavesUsed?: number;
  balance?: number;
}

export interface LeaveApplicationDto extends AuditedEntityDto<string> {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  leaveTypeId?: string;
  leaveTypeName?: string | null;
  fromDate?: string;
  toDate?: string;
  totalLeaveDays?: number;
  halfDay?: boolean;
  reason?: string | null;
  status?: LeaveApplicationStatus;
}

export interface LeaveTypeDetailDto extends EntityDto<string> {
  name?: string;
  maxDaysAllowed?: number;
  isActive?: boolean;
  requiresApproval?: boolean;
  allowCarryForward?: boolean;
  maxCarryForwardDays?: number;
  carryForwardExpiryMonths?: number;
  isPaidLeave?: boolean;
  includeHolidays?: boolean;
  allowNegativeBalance?: boolean;
}

export interface LeaveTypeDto {
  id?: string;
  name?: string;
  maxDaysAllowed?: number;
  isPaidLeave?: boolean;
  allowCarryForward?: boolean;
  requiresApproval?: boolean;
}

export interface LoanDto extends EntityDto<string> {
  companyId?: string;
  employeeId?: string;
  loanNumber?: string;
  loanType?: number;
  interestMethod?: number;
  status?: number;
  loanAmount?: number;
  annualInterestRate?: number;
  tenureMonths?: number;
  gracePeriodMonths?: number;
  emi?: number;
  totalAmountRepaid?: number;
  outstandingBalance?: number;
  disbursementDate?: string | null;
  repaymentStartDate?: string | null;
  schedule?: LoanRepaymentScheduleDto[];
}

export interface LoanRepaymentScheduleDto {
  paymentDate?: string;
  principalAmount?: number;
  interestAmount?: number;
  totalPayment?: number;
  outstandingBalance?: number;
  isPaid?: boolean;
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

export interface RecordRepaymentDto {
  principalAmount?: number;
  interestAmount?: number;
}

export interface SalaryComponentDto extends EntityDto<string> {
  name?: string;
  abbreviation?: string | null;
  componentType?: number;
  isStatutory?: boolean;
  isTaxApplicable?: boolean;
  dependsOnPaymentDays?: boolean;
  defaultAccountId?: string | null;
  isActive?: boolean;
  description?: string | null;
}

export interface SalarySlipDto extends EntityDto<string> {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  postingDate?: string;
  startDate?: string;
  endDate?: string;
  grossAmount?: number;
  totalDeductions?: number;
  netAmount?: number;
  status?: number;
}

export interface SalaryStructureDetailDto {
  id?: string;
  salaryComponentId?: string;
  componentName?: string;
  amount?: number;
  formula?: string | null;
}

export interface SalaryStructureDto extends EntityDto<string> {
  companyId?: string;
  name?: string;
  isHourlyBased?: boolean;
  payrollFrequency?: string;
  isActive?: boolean;
  description?: string | null;
  details?: SalaryStructureDetailDto[];
}
