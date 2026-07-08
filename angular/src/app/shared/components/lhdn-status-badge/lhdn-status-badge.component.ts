import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type LhdnStatus = 'Valid' | 'Invalid' | 'Submitted' | 'Cancelled' | 'Failed' | 'NotSubmitted';

interface LhdnStatusConfig {
  icon: string;
  badgeClass: string;
  label: string;
}

const LHDN_STATUS_MAP: Record<LhdnStatus, LhdnStatusConfig> = {
  Valid: { icon: 'fa fa-check-circle', badgeClass: 'bg-success', label: 'Valid' },
  Invalid: { icon: 'fa fa-times-circle', badgeClass: 'bg-danger', label: 'Invalid' },
  Submitted: { icon: 'fa fa-clock', badgeClass: 'bg-info', label: 'Submitted' },
  Cancelled: { icon: 'fa fa-ban', badgeClass: 'bg-secondary', label: 'Cancelled' },
  Failed: { icon: 'fa fa-exclamation-triangle', badgeClass: 'bg-warning text-dark', label: 'Failed' },
  NotSubmitted: { icon: 'fa fa-minus-circle', badgeClass: 'bg-secondary', label: 'Not Submitted' },
};

@Component({
  selector: 'app-lhdn-status-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './lhdn-status-badge.component.html',
  styleUrls: ['./lhdn-status-badge.component.scss'],
})
export class LhdnStatusBadgeComponent {
  @Input({ required: true }) status!: string;

  get config(): LhdnStatusConfig {
    return LHDN_STATUS_MAP[this.status as LhdnStatus] ?? LHDN_STATUS_MAP['NotSubmitted'];
  }
}
