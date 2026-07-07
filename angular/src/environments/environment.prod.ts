import { Environment } from '@abp/ng.core';

const baseUrl = 'http://localhost:4200';

const oAuthConfig = {
  issuer: 'https://localhost:44340/',
  redirectUri: baseUrl,
  clientId: 'MyERP_App',
  responseType: 'code',
  scope: 'offline_access MyERP',
  requireHttps: true,
};

export const environment = {
  production: true,
  application: {
    baseUrl,
    name: 'MyERP',
  },
  oAuthConfig,
  apis: {
    default: {
      url: 'https://localhost:44340',
      rootNamespace: 'MyERP',
    },
    AbpAccountPublic: {
      url: oAuthConfig.issuer,
      rootNamespace: 'AbpAccountPublic',
    },
  },
  remoteEnv: {
    url: '/getEnvConfig',
    mergeStrategy: 'deepmerge'
  }
} as Environment;
