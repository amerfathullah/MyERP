import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { LoanService } from '../../proxy/human-resources/loan.service';
import type { LoanDto } from '../../proxy/human-resources/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

const LOAN_STATUS = ['Draft', 'Sanctioned', 'Disbursed', 'PartiallyRepaid', 'FullyRepaid', 'Cancelled'] as const;

@Component({
  selector: 'app-loan-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'Loans' | abpLocalization">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div class="d-flex gap-2">
          <input type="text" class="form-control form-control-sm" style="width: 200px"
            [placeholder]="'::Placeholder:Search' | abpLocalization"
            [(ngModel)]="searchTerm" (keyup.enter)="onSearch()" />
          <select class="form-select form-select-sm" style="width: 150px" [(ngModel)]="statusFilter" (change)="onStatusChange()">
            <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
            <option value="Draft">Draft</option>
            <option value="Sanctioned">Sanctioned</option>
            <option value="Disbursed">Disbursed</option>
            <option value="PartiallyRepaid">Partially Repaid</option>
            <option value="FullyRepaid">Fully Repaid</option>
            <option value="Cancelled">Cancelled</option>
          </select>
        </div>
        <button class="btn btn-primary btn-sm" routerLink="/hr/loans/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewLoan' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-hand-holding-dollar fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoLoansYet' | abpLocalization }}</p>
          <button class="btn btn-primary btn-sm" routerLink="/hr/loans/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewLoan' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'LoanNumber' | abpLocalization }}</th>
              <th>{{ 'Employee' | abpLocalization }}</th>
              <th>{{ 'LoanType' | abpLocalization }}</th>
              <th class="text-end">{{ 'LoanAmount' | abpLocalization }}</th>
              <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (loan of items; track loan.id) {
                <tr>
                  <td><a [routerLink]="['/hr/loans', loan.id]">{{ loan.loanNumber }}</a></td>
                  <td>{{ loan.employeeId ?? '—' }}</td>
                  <td>{{ loan.loanType === 0 ? 'Term Loan' : 'Demand Loan' }}</td>
                  <td class="text-end">{{ loan.loanAmount | number:'1.2-2' }}</td>
                  <td class="text-end" [class.text-danger]="(loan.outstandingBalance ?? 0) > 0">{{ loan.outstandingBalance | number:'1.2-2' }}</td>
                  <td><app-status-badge [status]="statusLabel(loan.status)" /></td>
                  <td>
                    <a class="btn btn-sm btn-outline-primary" [routerLink]="['/hr/loans', loan.id]">
                      <i class="fa fa-eye"></i>
                    </a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class LoanListComponent implements OnInit {
  private loanService = inject(LoanService);

  items: LoanDto[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;
  searchTerm = '';
  statusFilter = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.loanService.getList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: '',
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined
    } as any).subscribe({
      next: res => {
        this.items = res.items ?? [];
        this.totalCount = res.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; }
    });
  }

  statusLabel(status: number | undefined): string { return LOAN_STATUS[status ?? 0]; }
  onSearch() { this.currentPage = 0; this.loadData(); }
  onStatusChange() { this.currentPage = 0; this.loadData(); }
  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.pageSize = e.pageSize; this.loadData(); }
}
