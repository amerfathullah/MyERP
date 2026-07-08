import type { EntityDto } from '@abp/ng.core';
import type { NotificationSeverity } from '../notification-severity.enum';

export interface AppNotificationDto extends EntityDto<string> {
  subject?: string;
  body?: string | null;
  severity?: NotificationSeverity;
  isRead?: boolean;
  readAt?: string | null;
  actionUrl?: string | null;
  sourceDocumentType?: string | null;
  sourceDocumentId?: string | null;
  creationTime?: string;
}

export interface NotificationSummaryDto {
  totalUnread?: number;
  recentNotifications?: AppNotificationDto[];
}
