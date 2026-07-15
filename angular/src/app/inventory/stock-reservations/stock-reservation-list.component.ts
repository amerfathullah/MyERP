import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface StockReservationEntry {
  id: string;
  itemId: string;
  warehouseId: string;
  voucherType: string;
  voucherId: string;
  reservedQty: number;
  deliveredQty: number;
  availableQty: number;
  status: number;
  creationTime: string;
}

@Component({
  selector: 'app-stock-reservation-list',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, FormsModule,
    PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'StockReservations' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'StockReservations' | abpLocalization }}</h5>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'Search' | abpLocalization"
                [(ngModel)]="searchTerm" (keyup.enter)="loadData()">
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (ngModelChange)="loadData()">
                <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
                <option value="Submitted">Active</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
          </div>

          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-lock fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoStockReservationsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'Item' | abpLocalization }}</th>
                <th>{{ 'Warehouse' | abpLocalization }}</th>
                <th>{{ 'VoucherType' | abpLocalization }}</th>
                <th class="text-end">{{ 'ReservedQty' | abpLocalization }}</th>
                <th class="text-end">{{ 'DeliveredQty' | abpLocalization }}</th>
                <th class="text-end">{{ 'AvailableQty' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th>{{ 'Date' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (sre of items(); track sre.id) {
                  <tr>
                    <td class="font-monospace text-truncate" style="max-width:120px">{{ sre.itemId | slice:0:8 }}…</td>
                    <td class="font-monospace text-truncate" style="max-width:120px">{{ sre.warehouseId | slice:0:8 }}…</td>
                    <td><span class="badge bg-info">{{ sre.voucherType }}</span></td>
                    <td class="text-end fw-semibold">{{ sre.reservedQty | number:'1.2-2' }}</td>
                    <td class="text-end">{{ sre.deliveredQty | number:'1.2-2' }}</td>
                    <td class="text-end" [class.text-danger]="sre.availableQty <= 0"
                        [class.text-success]="sre.availableQty > 0">
                      {{ sre.availableQty | number:'1.2-2' }}
                    </td>
                    <td><app-status-badge [status]="sre.status === 1 ? 'Submitted' : 'Cancelled'" /></td>
                    <td>{{ sre.creationTime | date:'dd/MM/yyyy' }}</td>
                    <td>
                      @if (sre.status === 1) {
                        <button class="btn btn-outline-danger btn-sm" (click)="cancelReservation(sre)">
                          <i class="fa fa-times"></i>
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class StockReservationListComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  items = signal<StockReservationEntry[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  searchTerm = '';
  statusFilter = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    const cid = this.companyContext.currentCompanyId();
    if (cid) params.companyId = cid;
    if (this.statusFilter) params.status = this.statusFilter;

    this.http.get<{ items: StockReservationEntry[]; totalCount: number }>(
      '/api/app/stock-reservation', { params }
    ).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  cancelReservation(sre: StockReservationEntry): void {
    if (!confirm('Cancel this reservation?')) return;
    this.http.post(`/api/app/stock-reservation/${sre.id}/cancel`, {}).subscribe({
      next: () => this.loadData(),
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
