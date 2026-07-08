import { mapEnumToOptions } from '@abp/ng.core';

export enum ExportFormat {
  Csv = 0,
  Excel = 1,
  Pdf = 2,
}

export const exportFormatOptions = mapEnumToOptions(ExportFormat);
