export interface ProjectDto {
  id?: string;
  projectNumber?: string;
  projectName?: string;
  status?: number;
  priority?: number;
  percentCompleteMethod?: number;
  percentComplete?: number;
  companyId?: string;
  customerId?: string;
  salesOrderId?: string;
  expectedStartDate?: string;
  expectedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  estimatedCost?: number;
  totalCostingAmount?: number;
  totalBillingAmount?: number;
  totalBilledAmount?: number;
  grossMargin?: number;
  notes?: string;
  taskCount?: number;
  creationTime?: string;
  lastModificationTime?: string;
}

export interface CreateProjectDto {
  projectName: string;
  priority?: number;
  percentCompleteMethod?: number;
  companyId: string;
  customerId?: string;
  salesOrderId?: string;
  expectedStartDate?: string;
  expectedEndDate?: string;
  estimatedCost?: number;
  notes?: string;
}

export interface UpdateProjectDto {
  projectName: string;
  priority?: number;
  percentCompleteMethod?: number;
  customerId?: string;
  expectedStartDate?: string;
  expectedEndDate?: string;
  estimatedCost?: number;
  notes?: string;
}

export interface ProjectTaskDto {
  id?: string;
  projectId?: string;
  taskNumber?: string;
  subject?: string;
  status?: number;
  priority?: number;
  parentTaskId?: string;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number;
  progress?: number;
  expectedStartDate?: string;
  expectedEndDate?: string;
  actualStartDate?: string;
  actualEndDate?: string;
  expectedHours?: number;
  actualHours?: number;
  assignedUserId?: string;
  description?: string;
}

export interface CreateProjectTaskDto {
  projectId: string;
  subject: string;
  priority?: number;
  parentTaskId?: string;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number;
  expectedStartDate?: string;
  expectedEndDate?: string;
  expectedHours?: number;
  assignedUserId?: string;
  description?: string;
}

export interface UpdateProjectTaskDto {
  subject: string;
  priority?: number;
  parentTaskId?: string;
  isGroup?: boolean;
  isMilestone?: boolean;
  taskWeight?: number;
  progress?: number;
  expectedStartDate?: string;
  expectedEndDate?: string;
  expectedHours?: number;
  assignedUserId?: string;
  description?: string;
}

export interface GetProjectListDto {
  status?: number;
  filter?: string;
  companyId?: string;
  skipCount?: number;
  maxResultCount?: number;
  sorting?: string;
}

export interface CreateTimesheetDto { [key: string]: any; }

export interface TimesheetDto { [key: string]: any; }

export interface UnbilledTimesheetSummaryDto { [key: string]: any; }
