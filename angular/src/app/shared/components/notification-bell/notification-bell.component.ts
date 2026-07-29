import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { NotificationService } from '../../../proxy/notification/notification.service';
import type { AppNotificationDto } from '../../../proxy/notification/dtos/models';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [
    CommonModule, RouterModule, LocalizationPipe
  ],
  templateUrl: './notification-bell.component.html',
  styleUrls: ['./notification-bell.component.scss'],
})
export class NotificationBellComponent implements OnInit {
  private service = inject(NotificationService);

  unreadCount = signal(0);
  notifications = signal<AppNotificationDto[]>([]);

  ngOnInit(): void {
    this.loadSummary();
    // Poll every 60 seconds
    setInterval(() => this.loadSummary(), 60000);
  }

  loadSummary(): void {
    this.service.getSummary().subscribe({
      next: (summary) => {
        this.unreadCount.set(summary.totalUnread);
        this.notifications.set(summary.recentNotifications ?? []);
      },
    });
  }

  markAsRead(id: string): void {
    this.service.markAsRead(id).subscribe({
      next: () => {
        this.notifications.update(list =>
          list.map(n => n.id === id ? { ...n, isRead: true } : n)
        );
        this.unreadCount.update(c => Math.max(0, c - 1));
      },
    });
  }

  markAllAsRead(): void {
    this.service.markAllAsRead().subscribe({
      next: () => {
        this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
        this.unreadCount.set(0);
      },
    });
  }

  getSeverityIcon(severity: number): string {
    const map: Record<number, string> = { 0: 'fa-circle-info', 1: 'fa-circle-check', 2: 'fa-triangle-exclamation', 3: 'fa-circle-xmark' };
    return map[severity] ?? 'fa-circle-info';
  }

  getSeverityColor(severity: number): string {
    const map: Record<number, string> = { 0: 'text-info', 1: 'text-success', 2: 'text-warning', 3: 'text-danger' };
    return map[severity] ?? 'text-info';
  }
}
