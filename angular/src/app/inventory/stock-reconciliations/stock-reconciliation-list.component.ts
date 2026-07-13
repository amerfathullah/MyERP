import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { StockReconciliationService } from '../../proxy/inventory/stock-reconciliation.service';
import type { StockReconciliationDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-stock-reconciliation-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './stock-reconciliation-list.component.html',
})
export class StockReconciliationListComponent implements OnInit {
  private service = inject(StockReconciliationService);
  reconciliations: StockReconciliationDto[] = [];
  totalCount = 0;
  isLoading = false;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 20 }).subscribe({
      next: (result) => {
        this.reconciliations = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', '', '', 'Cancelled'][status ?? 0] ?? 'Draft';
  }
}
