import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { BudgetService } from '../../proxy/accounting/budget.service';
import type { BudgetDto } from '../../proxy/dtos/models';

@Component({
  selector: 'app-budget-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent, PaginationComponent],
  templateUrl: './budget-list.component.html',
})
export class BudgetListComponent implements OnInit {
  private budgetService = inject(BudgetService);
  budgets: BudgetDto[] = [];
  totalCount = 0;
  isLoading = false;
  pageSize = 10;
  currentPage = 0;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.budgetService.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.budgets = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.load();
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', '', '', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  getTotalBudget(budget: BudgetDto): number {
    return (budget.accounts ?? []).reduce((sum, a) => sum + (a.budgetAmount ?? 0), 0);
  }
}
