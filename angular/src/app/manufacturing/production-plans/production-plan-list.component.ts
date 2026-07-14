import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanStore } from '../store/production-plan.store';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-production-plan-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './production-plan-list.component.html',
  styleUrls: ['./production-plan-list.component.scss'],
})
export class ProductionPlanListComponent implements OnInit {
  readonly store = inject(ProductionPlanStore);

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.store.load({ skipCount: 0, maxResultCount: 20 });
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'In Progress', 'Completed', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.store.load({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }); }
}