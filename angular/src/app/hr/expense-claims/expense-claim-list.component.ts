import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { ExpenseClaimService } from '../../proxy/human-resources/expense-claim.service';
import type { ExpenseClaimDto } from '../../proxy/human-resources/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-expense-claim-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'ExpenseClaims' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/hr/expense-claims/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewExpenseClaim' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-receipt fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoExpenseClaimsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Employee' | abpLocalization }}</th>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (ec of items; track ec.id) {
                <tr>
                  <td>{{ ec.employeeName ?? '—' }}</td>
                  <td>{{ ec.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ ec.expenseType ?? '—' }}</td>
                  <td class="text-end fw-bold">{{ ec.totalClaimedAmount | number:'1.2-2' }}</td>
                  <td><span class="badge" [ngClass]="statusClass(ec.status)">{{ statusLabel(ec.status) }}</span></td>
                  <td><a class="btn btn-sm btn-outline-primary" [routerLink]="['/hr/expense-claims', ec.id]"><i class="fa fa-eye"></i></a></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
      <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class ExpenseClaimListComponent implements OnInit {
  private service = inject(ExpenseClaimService);
  items: ExpenseClaimDto[] = [];
  isLoading = false;
  totalCount = 0;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize })
      .subscribe({ next: (r) => { this.items = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  statusLabel(s: number | undefined): string { return ['Draft', 'Submitted', 'Approved', '', 'Cancelled', 'Rejected'][s ?? 0] ?? 'Draft'; }
  statusClass(s: number | undefined): string { return ['bg-secondary', 'bg-primary', 'bg-success', '', 'bg-danger', 'bg-warning'][s ?? 0] ?? 'bg-secondary'; }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}