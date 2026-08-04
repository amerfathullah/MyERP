import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdatePrintFormatDto {
  name?: string;
  documentType?: string;
  htmlTemplate?: string;
  isDefault?: boolean;
}

export interface PrintFormatDto extends FullAuditedEntityDto<string> {
  name?: string;
  documentType?: string;
  htmlTemplate?: string;
  isDefault?: boolean;
}
