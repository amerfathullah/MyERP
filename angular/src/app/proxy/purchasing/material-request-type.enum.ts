import { mapEnumToOptions } from '@abp/ng.core';

export enum MaterialRequestType {
  Purchase = 0,
  MaterialTransfer = 1,
  MaterialIssue = 2,
  Manufacture = 3,
}

export const materialRequestTypeOptions = mapEnumToOptions(MaterialRequestType);
