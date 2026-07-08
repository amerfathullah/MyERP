import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatBadgeModule } from '@angular/material/badge';
import { MatMenuModule } from '@angular/material/menu';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { RouterModule } from '@angular/router';
import { NotificationService } from '../../../proxy/notification/notification.service';
import type { AppNotificationDto } from '../../../proxy/notification/models';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [
    CommonModule, MatBadgeModule,
    MatMenuModule, MatListModule, MatDividerModule, RouterModule
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
    const map: Record<number, string> = { 0: 'info', 1: 'check_circle', 2: 'warning', 3: 'error' };
    return map[severity] ?? 'info';
  }

  getSeverityColor(severity: number): string {
    const map: Record<number, string> = { 0: 'text-blue-500', 1: 'text-green-500', 2: 'text-yellow-500', 3: 'text-red-500' };
    return map[severity] ?? 'text-blue-500';
  }
}
