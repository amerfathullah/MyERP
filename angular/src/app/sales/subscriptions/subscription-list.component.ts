import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { SubscriptionService, type SubscriptionDto } from '../../proxy/sales/sales-advanced.service';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-subscription-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'Subscriptions' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/sales/subscriptions/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSubscription' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <app-loading-overlay /> }
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
      }
      <app-pagination [totalCount]="0" [pageSize]="pageSize" [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
  </abp-page>
  `,
})
export class SubscriptionListComponent implements OnInit {
  private service = inject(SubscriptionService);
  subscriptions: SubscriptionDto[] = [];
  isLoading = false;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 })
      .subscribe({ next: (r) => { this.subscriptions = r.items ?? []; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  getStatusLabel(s: number): string { return ['Active', 'Past Due', 'Unpaid', 'Cancelled', 'Completed'][s] ?? 'Active'; }
  getStatusClass(s: number): string { return ['bg-success', 'bg-warning', 'bg-danger', 'bg-secondary', 'bg-info'][s] ?? 'bg-success'; }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; /* reload handled by store */; }
}