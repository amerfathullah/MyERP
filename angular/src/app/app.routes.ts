import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes()),
  },
  {
    path: 'tenant-management',
    loadChildren: () => import('@abp/ng.tenant-management').then(c => c.createRoutes()),
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
  },
  {
    path: 'companies',
    loadComponent: () => import('./companies/company-list.component').then(c => c.CompanyListComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'customers',
    loadComponent: () => import('./customers/customer-list.component').then(c => c.CustomerListComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'invoices',
    loadComponent: () => import('./invoices/invoice-list.component').then(c => c.InvoiceListComponent),
    canActivate: [authGuard, permissionGuard],
  },
];
