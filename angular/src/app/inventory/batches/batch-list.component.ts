import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { BatchService } from '../../proxy/inventory/batch.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AvailableBatchItemDto, BatchDto } from '../../proxy/inventory/models';

import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-batch-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::Batches' | abpLocalization">
      <!-- Live Available Batch Lookup (BE: BatchService.getAvailableBatches) -->
      <div class="card mb-3">
        <div class="card-header py-2 d-flex justify-content-between align-items-center">
          <span class="fw-semibold small">
            <i class="fa fa-magnifying-glass me-1"></i>{{ '::AvailableBatches' | abpLocalization }}
          </span>
          @if (availableBatchesChecked()) {
            <button type="button" class="btn btn-sm btn-link text-decoration-none p-0" (click)="clearAvailability()">
              {{ '::Clear' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-5">
              <label class="form-label form-label-sm mb-1">{{ '::Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="filterItemId">
                <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                @for (it of itemOptions(); track it.id) {
                  <option [value]="it.id">{{ it.itemCode || it.itemName || it.id }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label form-label-sm mb-1">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="filterWarehouseId">
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (wh of warehouses(); track wh.id) {
                  <option [value]="wh.id">{{ wh.warehouseName || wh.name || wh.id }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <button type="button" class="btn btn-sm btn-outline-primary w-100"
                      [disabled]="checkingAvailability() || !filterItemId"
                      (click)="checkAvailability()">
                @if (checkingAvailability()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-check-circle me-1"></i>
                }
                {{ '::CheckAvailability' | abpLocalization }}
              </button>
            </div>
          </div>

          @if (availableBatchesChecked()) {
            @if (availableBatches().length === 0) {
              <p class="text-muted small mb-0 mt-2">{{ '::NoAvailableBatches' | abpLocalization }}</p>
            } @else {
              <div class="table-responsive mt-2">
                <table class="table table-sm table-hover mb-0">
                  <thead>
                    <tr>
                      <th>{{ '::BatchNo' | abpLocalization }}</th>
                      <th>{{ '::Warehouse' | abpLocalization }}</th>
                      <th class="text-end">{{ '::BalanceQuantity' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Reserved' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Available' | abpLocalization }}</th>
                      <th>{{ '::ExpiryDate' | abpLocalization }}</th>
                      <th>{{ '::Status' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (ab of availableBatches(); track ab.batchId || ab.batchNo) {
                      <tr>
                        <td class="font-monospace">{{ ab.batchNo }}</td>
                        <td>{{ ab.warehouseName || '—' }}</td>
                        <td class="text-end">{{ ab.balanceQuantity | number:'1.0-4' }}</td>
                        <td class="text-end text-muted">{{ ab.reservedQuantity | number:'1.0-4' }}</td>
                        <td class="text-end fw-semibold">{{ ab.availableQuantity | number:'1.0-4' }}</td>
                        <td>{{ ab.expiryDate ? (ab.expiryDate | date:'dd/MM/yyyy') : '—' }}</td>
                        <td>
                          @if (ab.isExpired) {
                            <span class="badge bg-danger">{{ '::Expired' | abpLocalization }}</span>
                          } @else {
                            <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          }
        </div>
      </div>

      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-boxes-stacked fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ '::NoBatchesYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ '::BatchNo' | abpLocalization }}</th>
              <th>{{ '::Item' | abpLocalization }}</th>
              <th>{{ '::ExpiryDate' | abpLocalization }}</th>
              <th>{{ '::Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (b of items; track b.id) {
                <tr>
                  <td class="font-monospace">{{ b.batchNo }}</td>
                  <td>{{ itemNames()[b.itemId ?? ''] || '—' }}</td>
                  <td>{{ b.expiryDate ? (b.expiryDate | date:'dd/MM/yyyy') : '—' }}</td>
                  <td>
                    @if (b.isDisabled) {
                      <span class="badge bg-secondary">Disabled</span>
                    } @else if (b.isExpired) {
                      <span class="badge bg-danger">Expired</span>
                    } @else {
                      <span class="badge bg-success">Active</span>
                    }
                  </td>
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
export class BatchListComponent implements OnInit {
  private service = inject(BatchService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);
  itemNames = signal<Record<string, string>>({});
  itemOptions = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  availableBatches = signal<AvailableBatchItemDto[]>([]);
  availableBatchesChecked = signal(false);
  checkingAvailability = signal(false);
  filterItemId = '';
  filterWarehouseId = '';
  items: BatchDto[] = [];
  isLoading = false;
  totalCount = 0;

  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.load();
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(r => {
      const list = r.items ?? [];
      this.itemOptions.set(list);
      const map: Record<string, string> = {};
      list.forEach((i: any) => { map[i.id] = i.itemCode || i.itemName || i.id; });
      this.itemNames.set(map);
    });
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(r => {
      this.warehouses.set((r.items ?? []).filter((w: any) => !w.isGroup));
    });
  }

  checkAvailability(): void {
    if (!this.filterItemId) return;
    this.checkingAvailability.set(true);
    this.service.getAvailableBatches({
      companyId: this.companyContext.currentCompanyId() || undefined,
      itemId: this.filterItemId,
      warehouseId: this.filterWarehouseId || undefined,
    }).subscribe({
      next: res => {
        this.availableBatches.set(res ?? []);
        this.availableBatchesChecked.set(true);
        this.checkingAvailability.set(false);
      },
      error: () => {
        this.availableBatches.set([]);
        this.availableBatchesChecked.set(true);
        this.checkingAvailability.set(false);
      },
    });
  }

  clearAvailability(): void {
    this.filterItemId = '';
    this.filterWarehouseId = '';
    this.availableBatches.set([]);
    this.availableBatchesChecked.set(false);
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize })
      .subscribe({ next: (r) => { this.items = r.items ?? []; this.totalCount = r.totalCount ?? 0; this.isLoading = false; }, error: () => { this.isLoading = false; } });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
