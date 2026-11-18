import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ProjectPriority } from './project-priority.enum';
import type { PercentCompleteMethod } from './percent-complete-method.enum';
import type { ProjectStatus } from './project-status.enum';
import type { TimesheetStatus } from './timesheet-status.enum';
import type { ProjectTaskStatus } from './project-task-status.enum';

export interface ActivityCostDto extends FullAuditedEntityDto<string> {
  employeeId?: string;
  employeeName?: string | null;
  department?: string | null;
  activityTypeId?: string;
  activityTypeName?: string | null;
  billingRate?: number;
  costingRate?: number;
}

export interface ActivityTypeDto extends EntityDto<string> {
  name?: string;
  defaultBillingRate?: number;
  defaultCostingRate?: number;
  isEnabled?: boolean;
}

export interface AddProjectUserDto {
  projectId: string;
  userId: string;
  viewAttachments?: boolean;
  hideTimesheets?: boolean;
}

export interface CreateActivityTypeDto {
  name: string;
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
  costCenterId?: string | null;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  estimatedCost?: number;
  notes?: string | null;
  collectProgress?: boolean;
  subject?: string | null;
  message?: string | null;
  projectTemplateId?: string | null;
}

export interface CreateProjectTaskDto {
  projectId: string;
  subject: string;
  priority?: ProjectPriority;
  parentTaskId?: string | null;
  taskTypeId?: string | null;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number | null;
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
  title?: string | null;
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

export interface CreateUpdateActivityCostDto {
  employeeId: string;
  activityTypeId: string;
  billingRate?: number;
  costingRate?: number;
}

export interface CreateUpdateProjectTemplateDto {
  templateName?: string;
  disabled?: boolean;
  tasks?: ProjectTemplateTaskDto[];
}

export interface CreateUpdateProjectTypeDto {
  name: string;
  isActive?: boolean;
}

export interface CreateUpdateProjectUpdateDto {
  projectId: string;
  date: string;
  time?: string | null;
  percentComplete?: number;
  summary?: string | null;
  notes?: string | null;
  sent?: boolean;
}

export interface CreateUpdateTaskTypeDto {
  name: string;
  weight?: number;
  description?: string | null;
}

export interface GetActivityCostListDto extends PagedAndSortedResultRequestDto {
  employeeId?: string | null;
  activityTypeId?: string | null;
}

export interface GetProjectListDto extends PagedAndSortedResultRequestDto {
  status?: ProjectStatus | null;
  filter?: string | null;
  companyId?: string | null;
}

export interface GetProjectUpdateListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  projectId?: string | null;
}

export interface GetTaskTypeListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GetTimesheetListDto extends PagedAndSortedResultRequestDto {
  companyId?: string | null;
  employeeId?: string | null;
  status?: TimesheetStatus | null;
  fromDate?: string | null;
  toDate?: string | null;
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
  totalConsumedMaterialCost?: number;
  grossMargin?: number;
  notes?: string | null;
  costCenterId?: string | null;
  collectProgress?: boolean;
  subject?: string | null;
  message?: string | null;
  taskCount?: number;
  users?: ProjectUserDto[];
}

export interface ProjectTaskDto {
  id?: string;
  projectId?: string;
  taskNumber?: string;
  subject?: string;
  status?: ProjectTaskStatus;
  priority?: ProjectPriority;
  parentTaskId?: string | null;
  taskTypeId?: string | null;
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

export interface ProjectTemplateDto extends EntityDto<string> {
  templateName?: string;
  disabled?: boolean;
  tasks?: ProjectTemplateTaskDto[];
}

export interface ProjectTemplateTaskDto {
  key?: string;
  subject?: string;
  taskTypeId?: string | null;
  taskWeight?: number;
  expectedHours?: number;
  isMilestone?: boolean;
  dependsOnKeys?: string[];
}

export interface ProjectTypeDto extends FullAuditedEntityDto<string> {
  name?: string;
  isActive?: boolean;
}

export interface ProjectUpdateDto extends FullAuditedEntityDto<string> {
  projectId?: string;
  projectNumber?: string | null;
  projectName?: string | null;
  date?: string;
  time?: string | null;
  percentComplete?: number;
  summary?: string | null;
  notes?: string | null;
  sent?: boolean;
}

export interface ProjectUserDto extends EntityDto<string> {
  projectId?: string;
  userId?: string;
  userName?: string | null;
  viewAttachments?: boolean;
  hideTimesheets?: boolean;
}

export interface SetActivityCostDto {
  employeeId: string;
  activityTypeId: string;
  billingRate?: number;
  costingRate?: number;
}

export interface TaskTypeDto extends FullAuditedEntityDto<string> {
  name?: string;
  weight?: number;
  description?: string | null;
}

export interface TimesheetBillingResultDto {
  invoiceId?: string;
  invoiceNumber?: string;
  totalHours?: number;
  totalAmount?: number;
  detailCount?: number;
}

export interface TimesheetDetailDto extends EntityDto<string> {
  timesheetId?: string;
  activityType?: string;
  fromTime?: string;
  toTime?: string;
  hours?: number;
  projectId?: string | null;
  taskId?: string | null;
  isBillable?: boolean;
  billingHours?: number;
  billingRate?: number;
  billingAmount?: number;
  costingRate?: number;
  costingAmount?: number;
  salesInvoiceId?: string | null;
  description?: string | null;
}

export interface TimesheetDto extends AuditedEntityDto<string> {
  companyId?: string;
  employeeId?: string;
  employeeName?: string | null;
  title?: string | null;
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
  costCenterId?: string | null;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  estimatedCost?: number;
  notes?: string | null;
  collectProgress?: boolean;
  subject?: string | null;
  message?: string | null;
}

export interface UpdateProjectTaskDto {
  subject: string;
  priority?: ProjectPriority;
  parentTaskId?: string | null;
  taskTypeId?: string | null;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number | null;
  progress?: number;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  expectedHours?: number;
  assignedUserId?: string | null;
  description?: string | null;
}

export interface UpdateProjectUserDto {
  viewAttachments?: boolean;
  hideTimesheets?: boolean;
}

export interface ProjectSummaryRequestDto {
  companyId?: string;
  status?: ProjectStatus | null;
  projectTypeId?: string | null;
  isActive?: boolean | null;
}

export interface ProjectSummaryRowDto {
  projectId?: string;
  projectNumber?: string;
  projectName?: string;
  projectTypeName?: string | null;
  status?: ProjectStatus;
  totalTasks?: number;
  completedTasks?: number;
  overdueTasks?: number;
  percentComplete?: number;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
}

export interface ProjectSummaryReportDto {
  totalProjects?: number;
  openProjects?: number;
  completedProjects?: number;
  totalTasks?: number;
  completedTasks?: number;
  overdueTasks?: number;
  rows?: ProjectSummaryRowDto[];
}

export interface DelayedTasksSummaryRequestDto {
  companyId?: string;
  projectId?: string | null;
  priority?: ProjectPriority | null;
  status?: ProjectTaskStatus | null;
  fromDate?: string | null;
  toDate?: string | null;
  asOfDate?: string | null;
}

export interface DelayedTaskSummaryRowDto {
  taskId?: string;
  taskNumber?: string;
  subject?: string;
  projectId?: string;
  projectNumber?: string;
  projectName?: string;
  status?: ProjectTaskStatus;
  priority?: ProjectPriority;
  progress?: number;
  expectedStartDate?: string | null;
  expectedEndDate?: string | null;
  actualEndDate?: string | null;
  delayInDays?: number;
}

export interface DelayedTasksSummaryReportDto {
  totalTasks?: number;
  delayedCount?: number;
  onTrackCount?: number;
  rows?: DelayedTaskSummaryRowDto[];
}

export interface DailyTimesheetSummaryRequestDto {
  companyId?: string;
  fromDate?: string | null;
  toDate?: string | null;
  employeeId?: string | null;
  projectId?: string | null;
}

export interface DailyTimesheetSummaryRowDto {
  timesheetId?: string;
  timesheetLabel?: string;
  employeeId?: string;
  employeeName?: string;
  fromTime?: string;
  toTime?: string;
  hours?: number;
  activityType?: string;
  taskId?: string | null;
  taskNumber?: string | null;
  taskSubject?: string | null;
  projectId?: string | null;
  projectNumber?: string | null;
  projectName?: string | null;
  status?: TimesheetStatus;
}

export interface DailyTimesheetSummaryReportDto {
  totalHours?: number;
  entryCount?: number;
  rows?: DailyTimesheetSummaryRowDto[];
}

export interface TimesheetBillingSummaryRequestDto {
  companyId?: string;
  employeeId?: string | null;
  projectId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  includeDraftTimesheets?: boolean;
}

export interface TimesheetBillingSummaryRowDto {
  date?: string;
  timesheetId?: string;
  timesheetLabel?: string;
  employeeId?: string;
  employeeName?: string;
  projectId?: string | null;
  projectNumber?: string | null;
  projectName?: string | null;
  workingHours?: number;
  billingHours?: number;
  billingAmount?: number;
}

export interface TimesheetBillingSummaryReportDto {
  currencyCode?: string;
  totalWorkingHours?: number;
  totalBillingHours?: number;
  totalBillingAmount?: number;
  rows?: TimesheetBillingSummaryRowDto[];
}
