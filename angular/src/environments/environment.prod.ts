import { Environment } from '@abp/ng.core';

const baseUrl = '';

const oAuthConfig = {
  issuer: '//',
  redirectUri: baseUrl || window.location.origin,
  clientId: 'MyERP_App',
  responseType: 'code',
  scope: 'offline_access MyERP',
  requireHttps: true,
};

export const environment = {
  production: true,
  application: {
    baseUrl: baseUrl || window.location.origin,
    name: 'MyERP',
  },
  oAuthConfig,
  apis: {
    default: {
      url: '',
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
