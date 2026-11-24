import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';

export type LhdnStatus = 'Valid' | 'Invalid' | 'Pending' | 'Submitted' | 'Cancelled' | 'Rejected' | 'Failed' | 'NotSubmitted' | 'Consolidated';

interface LhdnStatusConfig {
  icon: string;
  badgeClass: string;
  labelKey: string;
}

const LHDN_STATUS_MAP: Record<LhdnStatus, LhdnStatusConfig> = {
  Consolidated: { icon: 'fa fa-layer-group', badgeClass: 'bg-primary-subtle text-primary border border-primary-subtle', labelKey: '::Consolidated' },
  Valid: { icon: 'fa fa-check-circle', badgeClass: 'bg-success', labelKey: '::Valid' },
  Invalid: { icon: 'fa fa-times-circle', badgeClass: 'bg-danger', labelKey: '::Invalid' },
  Pending: { icon: 'fa fa-clock', badgeClass: 'bg-info', labelKey: '::Pending' },
  Submitted: { icon: 'fa fa-clock', badgeClass: 'bg-info', labelKey: '::Submitted' },
  Cancelled: { icon: 'fa fa-ban', badgeClass: 'bg-secondary', labelKey: '::Cancelled' },
  Rejected: { icon: 'fa fa-ban', badgeClass: 'bg-danger', labelKey: '::Rejected' },
  Failed: { icon: 'fa fa-exclamation-triangle', badgeClass: 'bg-warning text-dark', labelKey: '::Failed' },
  NotSubmitted: { icon: 'fa fa-minus-circle', badgeClass: 'bg-secondary', labelKey: '::NotSubmitted' },
};

@Component({
  selector: 'app-lhdn-status-badge',
  standalone: true,
  imports: [CommonModule, LocalizationPipe],
  templateUrl: './lhdn-status-badge.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./lhdn-status-badge.component.scss'],
})
export class LhdnStatusBadgeComponent {
  @Input({ required: true }) status!: string;
  @Input() isConsolidated?: boolean;

  get config(): LhdnStatusConfig {
    if (this.isConsolidated) {
      return LHDN_STATUS_MAP['Consolidated'];
    }
    return LHDN_STATUS_MAP[this.status as LhdnStatus] ?? LHDN_STATUS_MAP['NotSubmitted'];
  }
}
