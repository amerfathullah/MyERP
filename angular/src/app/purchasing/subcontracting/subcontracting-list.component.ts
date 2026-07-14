import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SubcontractingService, type SubcontractingOrderDto } from '../../proxy/manufacturing/additional-proxies.service';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-subcontracting-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'SubcontractingOrders' | abpLocalization">
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && orders.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-handshake fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSubcontractingOrdersYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'OrderNumber' | abpLocalization }}</th>
              <th>{{ 'Supplier' | abpLocalization }}</th>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th class="text-end">{{ 'Total' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (o of orders; track o.id) {
                <tr>
                  <td>{{ o.orderNumber ?? '—' }}</td>
                  <td>{{ o.supplierName ?? '—' }}</td>
                  <td>{{ o.transactionDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end fw-bold">{{ o.totalQty }}</td>
                  <td><app-status-badge [status]="getStatus(o.status)"></app-status-badge></td>
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
export class SubcontractingListComponent implements OnInit {
  private service = inject(SubcontractingService);
  orders: SubcontractingOrderDto[] = [];
  isLoading = false;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getOrderList({ skipCount: 0, maxResultCount: 50 })
      .subscribe({ next: (r) => { this.orders = r.items ?? []; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  getStatus(s: number): string { return ['Draft', 'Open', 'Partial', 'Completed', 'Closed', 'Cancelled'][s] ?? 'Draft'; }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; /* reload handled by store */; }
}