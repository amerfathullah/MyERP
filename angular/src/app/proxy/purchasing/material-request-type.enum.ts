import { mapEnumToOptions } from '@abp/ng.core';

export enum MaterialRequestType {
  Purchase = 0,
  MaterialTransfer = 1,
  MaterialIssue = 2,
  Manufacture = 3,
  CustomerProvided = 4,
  Subcontracting = 5,
}

export const materialRequestTypeOptions = mapEnumToOptions(MaterialRequestType);
