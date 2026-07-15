import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SubscriptionService } from '../../proxy/sales/subscription.service';
import type { SubscriptionDto } from '../../proxy/sales/models';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-subscription-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'Subscriptions' | abpLocalization">
      <div class="d-flex justify-content-between gap-2 mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="loadData()" placeholder="Search...">
        <button class="btn btn-primary btn-sm" routerLink="/sales/subscriptions/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSubscription' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <div class="text-center py-3"><i class="fa fa-spinner fa-spin fa-2x"></i></div> }
      @if (!isLoading && subscriptions.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-rotate fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSubscriptionsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'SubscriptionNumber' | abpLocalization }}</th>
              <th>{{ 'Party' | abpLocalization }}</th>
              <th>{{ 'BillingInterval' | abpLocalization }}</th>
              <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
              <th>{{ 'StartDate' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (sub of subscriptions; track sub.id) {
                <tr>
                  <td>{{ sub.subscriptionNumber ?? '—' }}</td>
                  <td>{{ sub.partyName ?? '—' }}</td>
                  <td>{{ sub.billingInterval }}</td>
                  <td class="text-end fw-bold">{{ sub.totalPerInterval | number:'1.2-2' }}</td>
                  <td>{{ sub.startDate | date:'dd/MM/yyyy' }}</td>
                  <td><span class="badge" [ngClass]="getStatusClass(sub.status)">{{ getStatusLabel(sub.status) }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `,
})
export class SubscriptionListComponent implements OnInit {
  private service = inject(SubscriptionService);
  subscriptions: SubscriptionDto[] = [];
  isLoading = false;
  searchTerm = '';
  totalCount = 0;
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    if (this.searchTerm) params.filter = this.searchTerm;
    this.service.getList(params)
      .subscribe({ next: (r) => { this.subscriptions = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  getStatusLabel(s: number | undefined): string { return ['Active', 'Past Due', 'Unpaid', 'Cancelled', 'Completed'][s ?? 0] ?? 'Active'; }
  getStatusClass(s: number | undefined): string { return ['bg-success', 'bg-warning', 'bg-danger', 'bg-secondary', 'bg-info'][s ?? 0] ?? 'bg-success'; }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
