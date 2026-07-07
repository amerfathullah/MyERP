import type { FullAuditedEntityDto } from '@abp/ng.core';
export interface AuthorDto extends FullAuditedEntityDto<string> {
  name?: string;
  birthDate?: string;
  shortBio?: string;
}
export interface AuthorExcelDownloadDto {
  downloadToken?: string;
  sorting?: string;
}
export interface CreateUpdateAuthorDto {
  name: string;
  birthDate: string;
  shortBio?: string;
}
export interface DownloadTokenResultDto {
  token?: string;
}
