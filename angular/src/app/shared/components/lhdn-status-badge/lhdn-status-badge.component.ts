import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

export type LhdnStatus = 'Valid' | 'Invalid' | 'Submitted' | 'Cancelled' | 'Failed' | 'NotSubmitted';

interface LhdnStatusConfig {
  icon: string;
  colorClass: string;
  label: string;
}

const LHDN_STATUS_MAP: Record<LhdnStatus, LhdnStatusConfig> = {
  Valid: { icon: 'verified', colorClass: 'bg-green-100 text-green-700', label: 'Valid' },
  Invalid: { icon: 'error', colorClass: 'bg-red-100 text-red-700', label: 'Invalid' },
  Submitted: { icon: 'schedule', colorClass: 'bg-blue-100 text-blue-700', label: 'Submitted' },
  Cancelled: { icon: 'cancel', colorClass: 'bg-gray-100 text-gray-600', label: 'Cancelled' },
  Failed: { icon: 'warning', colorClass: 'bg-orange-100 text-orange-700', label: 'Failed' },
  NotSubmitted: { icon: 'draft', colorClass: 'bg-gray-50 text-gray-400', label: 'Not Submitted' },
};

@Component({
  selector: 'app-lhdn-status-badge',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatIconModule],
  templateUrl: './lhdn-status-badge.component.html',
  styleUrls: ['./lhdn-status-badge.component.scss'],
})
export class LhdnStatusBadgeComponent {
  @Input({ required: true }) status!: string;

  get config(): LhdnStatusConfig {
    return LHDN_STATUS_MAP[this.status as LhdnStatus] ?? LHDN_STATUS_MAP['NotSubmitted'];
  }
}
