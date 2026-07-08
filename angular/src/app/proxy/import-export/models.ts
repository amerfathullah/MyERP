export interface ImportJobDto {
  id?: string;
  fileName?: string;
  entityType?: string;
  status?: number;
  totalRows?: number;
  successCount?: number;
  failureCount?: number;
  errorDetails?: string;
  companyId?: string;
  startedAt?: string;
  completedAt?: string;
  creationTime?: string;
}

export interface StartImportDto {
  entityType: string;
  fileName: string;
  fileContent: string;
  companyId?: string;
}

export interface ExportRequestDto {
  entityType: string;
  format?: number;
  companyId?: string;
  filterJson?: string;
}

export interface ExportResultDto {
  fileName?: string;
  contentType?: string;
  fileContent?: string;
}
