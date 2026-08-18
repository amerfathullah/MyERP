import type { CreateTaxWithholdingCategoryDto, TaxWithholdingCategoryDto, UpdateTaxWithholdingCategoryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class TaxWithholdingCategoryService {
  private restService = inject(RestService);
  apiName = 'Default';


  create = (input: CreateTaxWithholdingCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingCategoryDto>({
      method: 'POST',
      url: '/api/app/tax-withholding-category',
      body: input,
    },
    { apiName: this.apiName,...config });


  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tax-withholding-category/${id}`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingCategoryDto>({
      method: 'GET',
      url: `/api/app/tax-withholding-category/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TaxWithholdingCategoryDto>>({
      method: 'GET',
      url: '/api/app/tax-withholding-category',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  update = (id: string, input: UpdateTaxWithholdingCategoryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaxWithholdingCategoryDto>({
      method: 'PUT',
      url: `/api/app/tax-withholding-category/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
