import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type DocumentStatus = 'Draft' | 'Submitted' | 'Approved' | 'Posted' | 'Cancelled' | 'Rejected' | 'Overdue' | 'Paid' | 'PartiallyPaid' | 'Active' | 'Inactive';

interface StatusConfig {
  icon: string;
  badgeClass: string;
}

const STATUS_MAP: Record<DocumentStatus, StatusConfig> = {
  Draft: { icon: 'fa fa-file', badgeClass: 'bg-secondary' },
  Submitted: { icon: 'fa fa-paper-plane', badgeClass: 'bg-info' },
  Approved: { icon: 'fa fa-check-circle', badgeClass: 'bg-primary' },
  Posted: { icon: 'fa fa-check-double', badgeClass: 'bg-success' },
  Cancelled: { icon: 'fa fa-ban', badgeClass: 'bg-danger' },
  Rejected: { icon: 'fa fa-times-circle', badgeClass: 'bg-warning text-dark' },
  Overdue: { icon: 'fa fa-exclamation-triangle', badgeClass: 'bg-danger' },
  Paid: { icon: 'fa fa-check-circle', badgeClass: 'bg-success' },
  PartiallyPaid: { icon: 'fa fa-clock', badgeClass: 'bg-warning text-dark' },
  Active: { icon: 'fa fa-check-circle', badgeClass: 'bg-success' },
  Inactive: { icon: 'fa fa-minus-circle', badgeClass: 'bg-secondary' },
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-badge.component.html',
  styleUrls: ['./status-badge.component.scss'],
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: string;

  get config(): StatusConfig {
    return STATUS_MAP[this.status as DocumentStatus] ?? STATUS_MAP['Draft'];
  }
}
