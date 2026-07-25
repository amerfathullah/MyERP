import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import type { SubcontractingOrderDto } from '../../proxy/purchasing/models';

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
                  <td><a [routerLink]="['/purchasing/subcontracting', o.id]">{{ o.orderNumber ?? '—' }}</a></td>
                  <td>{{ supplierNames()[o.supplierId ?? ''] || o.supplierId || '—' }}</td>
                  <td>{{ o.orderDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end fw-bold">{{ o.grandTotal }}</td>
                  <td><app-status-badge [status]="getStatus(o.status)"></app-status-badge></td>
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
export class SubcontractingListComponent implements OnInit {
  private service = inject(SubcontractingService);
  private supplierService = inject(SupplierService);
  orders: SubcontractingOrderDto[] = [];
  isLoading = false;
  totalCount = 0;
  supplierNames = signal<Record<string, string>>({});

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.load();
    this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(res => {
      const map: Record<string, string> = {};
      (res.items ?? []).forEach((s: any) => { map[s.id] = s.name ?? s.id; });
      this.supplierNames.set(map);
    });
  }

  load(): void {
    this.isLoading = true;
    this.service.getOrderList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize })
      .subscribe({ next: (r) => { this.orders = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  getStatus(s: number | undefined): string { return ['Draft', 'Open', 'Partial', 'Completed', 'Closed', 'Cancelled'][s ?? 0] ?? 'Draft'; }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
