export interface AppNotificationDto {
  id?: string;
  subject?: string;
  body?: string;
  severity?: number;
  isRead?: boolean;
  readAt?: string;
  actionUrl?: string;
  sourceDocumentType?: string;
  sourceDocumentId?: string;
  creationTime?: string;
}

export interface NotificationSummaryDto {
  totalUnread?: number;
  recentNotifications?: AppNotificationDto[];
}
