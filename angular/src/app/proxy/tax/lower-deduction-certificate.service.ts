import type { CreateUpdateLowerDeductionCertificateDto, GetLowerDeductionCertificateListDto, LowerDeductionCertificateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LowerDeductionCertificateService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateUpdateLowerDeductionCertificateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LowerDeductionCertificateDto>({
      method: 'POST',
      url: '/api/app/lower-deduction-certificate',
      body: input,
    },
    { apiName: this.apiName, ...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/lower-deduction-certificate/${id}`,
    },
    { apiName: this.apiName, ...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LowerDeductionCertificateDto>({
      method: 'GET',
      url: `/api/app/lower-deduction-certificate/${id}`,
    },
    { apiName: this.apiName, ...config });


  getList = (input: GetLowerDeductionCertificateListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LowerDeductionCertificateDto>>({
      method: 'GET',
      url: '/api/app/lower-deduction-certificate',
      params: { companyId: input.companyId, supplierId: input.supplierId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });


  update = (id: string, input: CreateUpdateLowerDeductionCertificateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LowerDeductionCertificateDto>({
      method: 'PUT',
      url: `/api/app/lower-deduction-certificate/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });
}
