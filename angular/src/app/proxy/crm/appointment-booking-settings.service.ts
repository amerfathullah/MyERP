import type { AppointmentBookingSettingsDto, SaveAppointmentBookingSettingsDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentBookingSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  
  getForCompany = (companyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentBookingSettingsDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/app/appointment-booking-settings/for-company/${companyId}`,
    },
      { apiName: this.apiName,...config });
  

  
  save = (input: SaveAppointmentBookingSettingsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentBookingSettingsDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/app/appointment-booking-settings/save',
      body: input,
    },
      { apiName: this.apiName,...config });
}
