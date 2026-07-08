import type { ExportFormat } from '../export-format.enum';
import type { EntityDto } from '@abp/ng.core';
import type { ImportStatus } from '../import-status.enum';

export interface ExportRequestDto {
  entityType: string;
  format?: ExportFormat;
  companyId?: string | null;
  filterJson?: string | null;
}

export interface ExportResultDto {
  fileName?: string;
  contentType?: string;
  fileContent?: string;
}

export interface ImportJobDto extends EntityDto<string> {
  fileName?: string;
  entityType?: string;
  status?: ImportStatus;
  totalRows?: number;
  successCount?: number;
  failureCount?: number;
  errorDetails?: string | null;
  companyId?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  creationTime?: string;
}

export interface StartImportDto {
  entityType: string;
  fileName: string;
  fileContent: string;
  companyId?: string | null;
}
