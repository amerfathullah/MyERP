import { mapEnumToOptions } from '@abp/ng.core';

export enum IssueStatus {
  Open = 0,
  Replied = 1,
  OnHold = 2,
  Closed = 3,
  Cancelled = 4,
}

export const issueStatusOptions = mapEnumToOptions(IssueStatus);
