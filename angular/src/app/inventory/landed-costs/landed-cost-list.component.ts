import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { LandedCostVoucherService } from '../../proxy/inventory/landed-cost-voucher.service';
import type { LandedCostVoucherDto } from '../../proxy/dtos/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-landed-cost-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './landed-cost-list.component.html',
})
export class LandedCostListComponent implements OnInit {
  private service = inject(LandedCostVoucherService);
  vouchers: LandedCostVoucherDto[] = [];
  totalCount = 0;
  isLoading = false;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 20 }).subscribe({
      next: (result) => {
        this.vouchers = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', '', '', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  getDistMethodLabel(method: number | undefined): string {
    return ['By Quantity', 'By Amount', 'Manual'][method ?? 1] ?? 'By Amount';
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; /* reload handled by store */; }
}