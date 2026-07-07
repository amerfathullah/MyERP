import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';
export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];
function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
      {
        path: '/',
        name: '::Menu:Home',
        iconClass: 'fas fa-home',
        order: 1,
        layout: eLayoutType.application,
      },
      {
        path: '/companies',
        name: '::Menu:Companies',
        iconClass: 'fas fa-building',
        order: 2,
        layout: eLayoutType.application,
        requiredPolicy: 'MyERP.Companies',
      },
      {
        path: '/customers',
        name: '::Menu:Customers',
        iconClass: 'fas fa-users',
        order: 3,
        layout: eLayoutType.application,
        requiredPolicy: 'MyERP.Customers',
      },
      {
        path: '/invoices',
        name: 'Sales Invoices',
        iconClass: 'fas fa-file-invoice',
        order: 4,
        layout: eLayoutType.application,
        requiredPolicy: 'MyERP.SalesInvoices',
      },
  ]);
}
