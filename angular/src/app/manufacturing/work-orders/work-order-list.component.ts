import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { WorkOrderStore } from '../store/work-order.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  selector: 'app-work-order-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationModule, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './work-order-list.component.html',
  styleUrls: ['./work-order-list.component.scss'],
})
export class WorkOrderListComponent implements OnInit {
  readonly store = inject(WorkOrderStore);

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20, sorting: '' });
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Submitted', 'Not Started', 'In Process', 'Completed', 'Stopped', 'Cancelled'][status] ?? 'Draft';
  }
}
