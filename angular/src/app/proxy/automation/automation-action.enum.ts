import { mapEnumToOptions } from '@abp/ng.core';

export enum AutomationAction {
  SendNotification = 0,
  SendEmail = 1,
  SubmitToLhdn = 2,
  CreateApprovalRequest = 3,
  UpdateField = 4,
  CreateFollowUpTask = 5,
  PostToAccounting = 6,
}

export const automationActionOptions = mapEnumToOptions(AutomationAction);
