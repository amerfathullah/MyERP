import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

export type DocumentStatus = 'Draft' | 'Submitted' | 'Approved' | 'Posted' | 'Cancelled' | 'Rejected' | 'Overdue' | 'Paid' | 'PartiallyPaid' | 'Active' | 'Inactive' | 'Pending' | 'ToDeliverAndBill' | 'ToDeliver' | 'ToBill' | 'Completed' | 'Closed' | 'NotApplicable' | 'Unfulfilled' | 'PartiallyFulfilled' | 'Fulfilled' | 'Lapsed' | 'Return' | 'ReturnIssued' | 'PartiallyBilled' | 'Ordered' | 'PartiallyOrdered' | 'PartiallyReceived' | 'Partial' | 'Open' | 'Lost' | 'Converted' | 'Quotation' | 'ToPay';

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
  Pending: { icon: 'fa fa-clock', badgeClass: 'bg-warning text-dark' },
  ToDeliverAndBill: { icon: 'fa fa-truck', badgeClass: 'bg-info' },
  ToDeliver: { icon: 'fa fa-truck', badgeClass: 'bg-warning text-dark' },
  ToBill: { icon: 'fa fa-file-invoice', badgeClass: 'bg-warning text-dark' },
  ToPay: { icon: 'fa fa-money-bill-wave', badgeClass: 'bg-info' },
  Completed: { icon: 'fa fa-check-double', badgeClass: 'bg-success' },
  Closed: { icon: 'fa fa-lock', badgeClass: 'bg-dark' },
  NotApplicable: { icon: 'fa fa-minus', badgeClass: 'bg-secondary' },
  Unfulfilled: { icon: 'fa fa-hourglass-start', badgeClass: 'bg-warning text-dark' },
  PartiallyFulfilled: { icon: 'fa fa-hourglass-half', badgeClass: 'bg-info' },
  Fulfilled: { icon: 'fa fa-check-double', badgeClass: 'bg-success' },
  Lapsed: { icon: 'fa fa-exclamation-triangle', badgeClass: 'bg-danger' },
  Return: { icon: 'fa fa-undo', badgeClass: 'bg-warning text-dark' },
  ReturnIssued: { icon: 'fa fa-undo', badgeClass: 'bg-info' },
  PartiallyBilled: { icon: 'fa fa-clock', badgeClass: 'bg-warning text-dark' },
  Ordered: { icon: 'fa fa-check-double', badgeClass: 'bg-success' },
  PartiallyOrdered: { icon: 'fa fa-clock', badgeClass: 'bg-info' },
  PartiallyReceived: { icon: 'fa fa-clock', badgeClass: 'bg-info' },
  Partial: { icon: 'fa fa-clock', badgeClass: 'bg-info' },
  Open: { icon: 'fa fa-folder-open', badgeClass: 'bg-info' },
  Lost: { icon: 'fa fa-thumbs-down', badgeClass: 'bg-warning text-dark' },
  Converted: { icon: 'fa fa-check-circle', badgeClass: 'bg-success' },
  Quotation: { icon: 'fa fa-file-invoice-dollar', badgeClass: 'bg-info' },
};

const NUMERIC_DOCUMENT_STATUS_LABELS: Record<number, string> = {
  0: 'Draft',
  1: 'Submitted',
  2: 'Approved',
  3: 'Posted',
  4: 'Cancelled',
  5: 'Rejected',
  10: 'ToDeliverAndBill',
  11: 'ToDeliver',
  12: 'ToBill',
  13: 'Completed',
  14: 'Closed',
  15: 'PartiallyBilled',
  16: 'ToPay',
};

/** Maps numeric MyERP.Core.DocumentStatus values (or string passthrough) to badge labels. */
export function getDocumentStatusLabel(status?: number | string | null, submittedLabel = 'Submitted'): string {
  if (typeof status === 'string') return status || 'Draft';
  if (status === 1) return submittedLabel;
  return NUMERIC_DOCUMENT_STATUS_LABELS[status ?? 0] ?? 'Draft';
}

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  templateUrl: './status-badge.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./status-badge.component.scss'],
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: string | number | null | undefined;

  private get normalizedKey(): string {
    if (typeof this.status === 'number') {
      return getDocumentStatusLabel(this.status);
    }
    const raw = typeof this.status === 'string'
      ? this.status
      : (this.status !== null && this.status !== undefined ? String(this.status) : 'Draft');
    return raw ? raw.replace(/\s+/g, '') : 'Draft';
  }

  get config(): StatusConfig {
    const key = this.normalizedKey as DocumentStatus;
    return STATUS_MAP[key] ?? STATUS_MAP[String(this.status) as DocumentStatus] ?? STATUS_MAP['Draft'];
  }

  get labelKey(): string | null {
    const key = this.normalizedKey as DocumentStatus;
    return key in STATUS_MAP ? `::${key}` : null;
  }
}
