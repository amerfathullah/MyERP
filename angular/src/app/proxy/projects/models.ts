import type { ProjectPriority } from './project-priority.enum';
import type { PercentCompleteMethod } from './percent-complete-method.enum';
import type { AuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ProjectStatus } from './project-status.enum';
import type { TimesheetStatus } from './entities/timesheet-status.enum';
import type { ProjectTaskStatus } from './project-task-status.enum';

export interface ActivityCostDto {
  id?: string;
  employeeId?: string;
  activityTypeId?: string;
  billingRate?: number;
  costingRate?: number;
}

export interface ActivityTypeDto {
  id?: string;
  name?: string;
  defaultBillingRate?: number;
  defaultCostingRate?: number;
  isEnabled?: boolean;
}

export interface CreateActivityTypeDto {
  name?: string;
  defaultBillingRate?: number;
  defaultCostingRate?: number;
}

export interface CreateProjectDto {
  projectName: string;
  priority?: ProjectPriority;
  percentCompleteMethod?: PercentCompleteMethod;
  companyId: string;
  customerId?: string | null;
  salesOrderId?: string | null;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  estimatedCost?: number;
  notes?: string | null;
}

export interface CreateProjectTaskDto {
  projectId: string;
  subject: string;
  priority?: ProjectPriority;
  parentTaskId?: string | null;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  expectedHours?: number;
  assignedUserId?: string | null;
  description?: string | null;
}

export interface CreateTimesheetDetailDto {
  activityType: string;
  fromTime: string;
  toTime: string;
  hours: number;
  projectId?: string | null;
  taskId?: string | null;
  isBillable?: boolean;
  billingRate?: number;
  costingRate?: number;
  description?: string | null;
}

export interface CreateTimesheetDto {
  companyId: string;
  employeeId: string;
  employeeName?: string | null;
  startDate: string;
  endDate: string;
  note?: string | null;
  details?: CreateTimesheetDetailDto[];
}

export interface CreateTimesheetInvoiceDto {
  companyId: string;
  customerId: string;
  projectId?: string | null;
}

export interface GetProjectListDto extends PagedAndSortedResultRequestDto {
  status?: ProjectStatus | null;
  filter?: string | null;
  companyId?: string | null;
}

export interface GetTimesheetListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  employeeId?: string | null;
  status?: TimesheetStatus | null;
  filter?: string | null;
}

export interface ProjectDto extends AuditedEntityDto<string> {
  projectNumber?: string;
  projectName?: string;
  status?: ProjectStatus;
  priority?: ProjectPriority;
  percentCompleteMethod?: PercentCompleteMethod;
  percentComplete?: number;
  companyId?: string;
  customerId?: string | null;
  salesOrderId?: string | null;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  estimatedCost?: number;
  totalCostingAmount?: number;
  totalBillingAmount?: number;
  totalBilledAmount?: number;
  grossMargin?: number;
  notes?: string | null;
  taskCount?: number;
}

export interface ProjectTaskDto {
  id?: string;
  projectId?: string;
  taskNumber?: string;
  subject?: string;
  status?: ProjectTaskStatus;
  priority?: ProjectPriority;
  parentTaskId?: string | null;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number;
  progress?: number;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  actualStartDate?: string | null;
  actualEndDate?: string | null;
  expectedHours?: number;
  actualHours?: number;
  assignedUserId?: string | null;
  description?: string | null;
}

export interface SetActivityCostDto {
  employeeId?: string;
  activityTypeId?: string;
  billingRate?: number;
  costingRate?: number;
}

export interface TimesheetBillingResultDto {
  invoiceId?: string;
  invoiceNumber?: string;
  totalHours?: number;
  totalAmount?: number;
  detailCount?: number;
}

export interface TimesheetDetailDto {
  id?: string;
  activityType?: string;
  fromTime?: string;
  toTime?: string;
  hours?: number;
  projectId?: string | null;
  taskId?: string | null;
  isBillable?: boolean;
  billingRate?: number;
  billingAmount?: number;
  costingRate?: number;
  costingAmount?: number;
  description?: string | null;
}

export interface TimesheetDto extends AuditedEntityDto<string> {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  status?: TimesheetStatus;
  startDate?: string;
  endDate?: string;
  totalHours?: number;
  totalBillableHours?: number;
  totalBillingAmount?: number;
  totalCostingAmount?: number;
  note?: string | null;
  details?: TimesheetDetailDto[];
}

export interface UnbilledTimesheetSummaryDto {
  activityType?: string;
  totalHours?: number;
  totalAmount?: number;
  entryCount?: number;
}

export interface UpdateActivityTypeDto {
  defaultBillingRate?: number;
  defaultCostingRate?: number;
  isEnabled?: boolean;
}

export interface UpdateProjectDto {
  projectName: string;
  priority?: ProjectPriority;
  percentCompleteMethod?: PercentCompleteMethod;
  customerId?: string | null;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  estimatedCost?: number;
  notes?: string | null;
}

export interface UpdateProjectTaskDto {
  subject: string;
  priority?: ProjectPriority;
  parentTaskId?: string | null;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number;
  progress?: number;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  expectedHours?: number;
  assignedUserId?: string | null;
  description?: string | null;
}
