import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { BlanketOrderService, type BlanketOrderDto } from '../../proxy/sales/additional-proxies.service';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-blanket-order-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'BlanketOrders' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/sales/blanket-orders/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewBlanketOrder' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && orders.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-file-contract fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoBlanketOrdersYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'OrderNumber' | abpLocalization }}</th>
              <th>{{ 'Party' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th>{{ 'ValidFrom' | abpLocalization }}</th>
              <th>{{ 'ValidUntil' | abpLocalization }}</th>
              <th>{{ 'Items' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (bo of orders; track bo.id) {
                <tr>
                  <td>{{ bo.orderNumber }}</td>
                  <td>{{ bo.partyName ?? '—' }}</td>
                  <td><span class="badge bg-info">{{ bo.orderType }}</span></td>
                  <td>{{ bo.fromDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ bo.toDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ (bo.items ?? []).length }}</td>
                  <td><span class="badge" [ngClass]="{'bg-secondary': bo.status===0, 'bg-success': bo.status===1, 'bg-danger': bo.status===4}">
                    {{ ['Draft','Active','','','Cancelled'][bo.status] }}
                  </span></td>
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
export class BlanketOrderListComponent implements OnInit {
  private service = inject(BlanketOrderService);
  orders: BlanketOrderDto[] = [];
  isLoading = false;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 })
      .subscribe({ next: (r) => { this.orders = r.items ?? []; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; /* reload handled by store */; }
}