import type { AuditedEntityDto, EntityDto } from '@abp/ng.core';
import type { BookType } from './book-type.enum';
export interface AuthorLookupDto extends EntityDto<string> {
  name?: string;
}
export interface BookDto extends AuditedEntityDto<string> {
  name?: string;
  authorId?: string;
  authorName?: string;
  type: BookType;
  publishDate?: string;
  price: number;
}
export interface BookExcelDownloadDto {
  downloadToken?: string;
  sorting?: string;
}
export interface CreateUpdateBookDto {
  name: string;
  authorId: string;
  type: BookType;
  publishDate: string;
  price: number;
}
export interface DownloadTokenResultDto {
  token?: string;
}
