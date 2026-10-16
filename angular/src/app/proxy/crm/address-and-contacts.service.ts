import type {
  GetAddressAndContactsInput,
  AddressAndContactsReportDto,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AddressAndContactsService {
  private restService = inject(RestService);
  apiName = 'Default';

  getReport = (
    input: GetAddressAndContactsInput,
    config?: Partial<Rest.Config>
  ) =>
    this.restService.request<any, AddressAndContactsReportDto>(
      {
        method: 'GET',
        url: '/api/app/address-and-contacts/report',
        params: {
          companyId: input.companyId,
          partyType: input.partyType,
          partyId: input.partyId,
          search: input.search,
        },
      },
      { apiName: this.apiName, ...config }
    );
}
