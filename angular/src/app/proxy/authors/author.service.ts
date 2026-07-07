import type { AuthorDto, AuthorExcelDownloadDto, CreateUpdateAuthorDto, DownloadTokenResultDto } from './models';
import { RestService } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable } from '@angular/core';
@Injectable({ providedIn: 'root' })
export class AuthorService {
  apiName = 'Default';
  create = (input: CreateUpdateAuthorDto) => this.restService.request<any, AuthorDto>({ method: 'POST', url: '/api/app/author', body: input }, { apiName: this.apiName });
  delete = (id: string) => this.restService.request<any, void>({ method: 'DELETE', url: `/api/app/author/${id}` }, { apiName: this.apiName });
  get = (id: string) => this.restService.request<any, AuthorDto>({ method: 'GET', url: `/api/app/author/${id}` }, { apiName: this.apiName });
  getDownloadToken = () => this.restService.request<any, DownloadTokenResultDto>({ method: 'GET', url: '/api/app/author/download-token' }, { apiName: this.apiName });
  getList = (input: PagedAndSortedResultRequestDto) => this.restService.request<any, PagedResultDto<AuthorDto>>({ method: 'GET', url: '/api/app/author', params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount } }, { apiName: this.apiName });
  getListAsExcelFile = (input: AuthorExcelDownloadDto) => this.restService.request<any, Blob>({ method: 'GET', responseType: 'blob', url: '/api/app/author/as-excel-file', params: { downloadToken: input.downloadToken, sorting: input.sorting } }, { apiName: this.apiName });
  update = (id: string, input: CreateUpdateAuthorDto) => this.restService.request<any, AuthorDto>({ method: 'PUT', url: `/api/app/author/${id}`, body: input }, { apiName: this.apiName });
  constructor(private restService: RestService) {}
}
