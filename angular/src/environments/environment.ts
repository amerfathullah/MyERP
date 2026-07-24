import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'http://localhost:5001/',
  redirectUri: baseUrl,
  clientId: 'MyERP_App',
  responseType: 'code',
  scope: 'offline_access MyERP',
  requireHttps: false,
};

export const environment = {
  production: false,
  application: {
    baseUrl,
    name: 'MyERP',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'http://localhost:5001',
      rootNamespace: 'MyERP',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
} as Environment;
