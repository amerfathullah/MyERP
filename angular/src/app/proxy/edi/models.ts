import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CodeListDto extends FullAuditedEntityDto<string> {
  title?: string;
  canonicalUri?: string | null;
  url?: string | null;
  defaultCommonCode?: string | null;
  version?: string | null;
  publisher?: string | null;
  publisherId?: string | null;
  description?: string | null;
  isActive?: boolean;
}

export interface CommonCodeDto extends FullAuditedEntityDto<string> {
  codeListId?: string;
  title?: string;
  code?: string;
  description?: string | null;
  additionalDataJson?: string | null;
  isActive?: boolean;
}

export interface CreateUpdateCodeListDto {
  title: string;
  canonicalUri?: string | null;
  url?: string | null;
  defaultCommonCode?: string | null;
  version?: string | null;
  publisher?: string | null;
  publisherId?: string | null;
  description?: string | null;
  isActive?: boolean;
}

export interface CreateUpdateCommonCodeDto {
  codeListId: string;
  title: string;
  code: string;
  description?: string | null;
  additionalDataJson?: string | null;
  isActive?: boolean;
}

export interface GetCodeListListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  publisher?: string | null;
  isActive?: boolean | null;
}

export interface GetCommonCodeListDto extends PagedAndSortedResultRequestDto {
  codeListId?: string | null;
  filter?: string | null;
  isActive?: boolean | null;
}
