import type { PrintFormatType } from './print-format-type.enum';
import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdatePrintFormatDto {
  name?: string;
  documentType?: string;
  htmlTemplate?: string;
  isDefault?: boolean;
  formatType?: PrintFormatType;
  formatData?: string | null;
}

export interface PrintFormatDto extends FullAuditedEntityDto<string> {
  name?: string;
  documentType?: string;
  htmlTemplate?: string;
  isDefault?: boolean;
  formatType?: PrintFormatType;
  formatData?: string | null;
}
