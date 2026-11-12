import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SalesOrderAmendmentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  amend = (cancelledOrderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/app/sales-order-amendment/amend/${cancelledOrderId}`,
    },
      { apiName: this.apiName,...config });
}
