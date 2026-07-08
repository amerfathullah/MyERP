import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

export type DocumentStatus = 'Draft' | 'Submitted' | 'Approved' | 'Posted' | 'Cancelled' | 'Rejected' | 'Overdue' | 'Paid' | 'PartiallyPaid' | 'Active' | 'Inactive';

interface StatusConfig {
  icon: string;
  colorClass: string;
}

const STATUS_MAP: Record<DocumentStatus, StatusConfig> = {
  Draft: { icon: 'edit_note', colorClass: 'bg-gray-100 text-gray-700' },
  Submitted: { icon: 'send', colorClass: 'bg-blue-100 text-blue-700' },
  Approved: { icon: 'check_circle', colorClass: 'bg-indigo-100 text-indigo-700' },
  Posted: { icon: 'verified', colorClass: 'bg-green-100 text-green-700' },
  Cancelled: { icon: 'cancel', colorClass: 'bg-red-100 text-red-700' },
  Rejected: { icon: 'block', colorClass: 'bg-orange-100 text-orange-700' },
  Overdue: { icon: 'warning', colorClass: 'bg-red-100 text-red-700' },
  Paid: { icon: 'payments', colorClass: 'bg-green-100 text-green-700' },
  PartiallyPaid: { icon: 'hourglass_top', colorClass: 'bg-yellow-100 text-yellow-700' },
  Active: { icon: 'check_circle', colorClass: 'bg-green-100 text-green-700' },
  Inactive: { icon: 'pause_circle', colorClass: 'bg-gray-100 text-gray-500' },
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatIconModule],
  templateUrl: './status-badge.component.html',
  styleUrls: ['./status-badge.component.scss'],
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: DocumentStatus;

  get config(): StatusConfig {
    return STATUS_MAP[this.status] ?? STATUS_MAP['Draft'];
  }
}
