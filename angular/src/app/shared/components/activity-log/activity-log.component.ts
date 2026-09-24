import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { DocumentActivityLogService } from '../../../proxy/core/document-activity-log.service';
import type { DocumentActivityLogDto } from '../../../proxy/core/models';

const ACTIVITY_ICONS: Record<string, string> = {
  Submitted: 'fa fa-paper-plane text-info',
  Posted: 'fa fa-check-double text-success',
  Cancelled: 'fa fa-ban text-danger',
  Converted: 'fa fa-exchange-alt text-primary',
  PaymentReceived: 'fa fa-money-bill-wave text-success',
  WriteOff: 'fa fa-eraser text-warning',
  Amended: 'fa fa-copy text-secondary',
};

@Component({
  selector: 'app-activity-log',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  templateUrl: './activity-log.component.html',
  styleUrl: './activity-log.component.scss',
})
export class ActivityLogComponent implements OnInit {
  @Input() documentType!: string;
  @Input() documentId!: string;

  private activityLogService: DocumentActivityLogService | null = null;

  logs: DocumentActivityLogDto[] = [];
  loading = false;

  constructor() {
    try {
      this.activityLogService = inject(DocumentActivityLogService);
    } catch {
      // Allows instantiation in unit tests without Angular injection context
    }
  }

  ngOnInit(): void {
    if (this.documentType && this.documentId && this.documentId !== 'new') {
      this.loadLogs();
    }
  }

  loadLogs(): void {
    // Per ERPNext PR #59386 (commit 87052020fb): unsaved doc has no activities and cannot be permission-checked
    if (!this.documentType || !this.documentId || this.documentId === 'new') {
      this.logs = [];
      return;
    }

    this.loading = true;
    this.activityLogService
      .getForDocument(this.documentType, this.documentId)
      .subscribe({
        next: (logs) => {
          this.logs = logs;
          this.loading = false;
        },
        error: () => {
          this.loading = false;
        },
      });
  }

  getIcon(activityType: string): string {
    return ACTIVITY_ICONS[activityType] || 'fa fa-circle text-secondary';
  }

  formatTime(isoDate: string): string {
    return new Date(isoDate).toLocaleString();
  }
}
