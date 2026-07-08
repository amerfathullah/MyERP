import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { LhdnDashboardStore } from '../store/lhdn-dashboard.store';

interface StatCard {
  label: string;
  count: number;
  textClass: string;
  icon: string;
}

@Component({
  selector: 'app-lhdn-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatIconModule,
    MatTableModule,
    MatButtonModule,
    LhdnStatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './lhdn-dashboard.component.html',
  styleUrls: ['./lhdn-dashboard.component.scss'],
})
export class LhdnDashboardComponent implements OnInit {
  readonly store = inject(LhdnDashboardStore);

  get statusCards(): StatCard[] {
    const stats = this.store.salesStats();
    return [
      { label: 'Valid', count: stats.valid, textClass: 'text-green-600', icon: 'verified' },
      { label: 'Invalid', count: stats.invalid, textClass: 'text-red-600', icon: 'error' },
      { label: 'Submitted', count: stats.submitted, textClass: 'text-blue-600', icon: 'schedule' },
      { label: 'Cancelled', count: stats.cancelled, textClass: 'text-gray-500', icon: 'cancel' },
      { label: 'Failed', count: stats.failed, textClass: 'text-orange-600', icon: 'warning' },
      { label: 'Not Submitted', count: stats.notSubmitted, textClass: 'text-gray-400', icon: 'draft' },
    ];
  }

  ngOnInit(): void {
    // TODO: Wire up to EInvoiceDashboardAppService proxy via rxMethod
    // For now, load mock data to verify UI
    this.store.loadSuccess(
      { valid: 24, invalid: 3, submitted: 5, cancelled: 1, failed: 2, notSubmitted: 12 },
      { valid: 18, invalid: 1, submitted: 3, cancelled: 0, failed: 1, notSubmitted: 8 },
      [
        { month: 'Jan', valid: 15, invalid: 2, submitted: 3 },
        { month: 'Feb', valid: 18, invalid: 1, submitted: 4 },
        { month: 'Mar', valid: 22, invalid: 3, submitted: 2 },
        { month: 'Apr', valid: 20, invalid: 0, submitted: 5 },
        { month: 'May', valid: 24, invalid: 2, submitted: 3 },
        { month: 'Jun', valid: 24, invalid: 3, submitted: 5 },
      ],
    );
  }
}
