import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { BatchService } from '../../proxy/inventory/batch.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import type { BatchStockBalanceDto, BatchMovementHistoryDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-batch-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <div class="container-fluid">
      <app-breadcrumb />
      @if (batch(); as b) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">
              <i class="fas fa-layer-group me-2"></i>{{ b.batchNumber }}
              @if (b.disabled) {
                <span class="badge bg-danger ms-2">{{ 'Disabled' | abpLocalization }}</span>
              } @else if (isExpired()) {
                <span class="badge bg-warning text-dark ms-2">{{ 'Expired' | abpLocalization }}</span>
              } @else {
                <span class="badge bg-success ms-2">{{ 'Active' | abpLocalization }}</span>
              }
            </h5>
            @if (!b.disabled) {
              <button class="btn btn-outline-danger btn-sm" (click)="disable()">
                <i class="fas fa-ban me-1"></i>{{ 'Disable' | abpLocalization }}
              </button>
            }
          </div>
          <div class="card-body">
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Item' | abpLocalization }}</small>
                <div class="fw-bold">{{ b.itemName || b.itemId }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'ExpiryDate' | abpLocalization }}</small>
                <div>
                  @if (b.expiryDate) {
                    <span [class]="isExpired() ? 'text-danger fw-bold' : ''">
                      {{ b.expiryDate | date:'dd/MM/yyyy' }}
                    </span>
                    @if (daysUntilExpiry() !== null) {
                      <small class="ms-1" [class]="daysUntilExpiry()! < 0 ? 'text-danger' : daysUntilExpiry()! < 30 ? 'text-warning' : 'text-muted'">
                        ({{ daysUntilExpiry()! < 0 ? 'Expired ' + (-daysUntilExpiry()!) + ' days ago' : daysUntilExpiry() + ' days remaining' }})
                      </small>
                    }
                  } @else {
                    <span class="text-muted">{{ 'NoExpiry' | abpLocalization }}</span>
                  }
                </div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'ManufacturingDate' | abpLocalization }}</small>
                <div>{{ b.manufacturingDate ? (b.manufacturingDate | date:'dd/MM/yyyy') : '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'ShelfLifeDays' | abpLocalization }}</small>
                <div>{{ b.shelfLifeDays ?? '—' }}</div>
              </div>
            </div>
            <div class="row">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Supplier' | abpLocalization }}</small>
                <div>{{ b.supplierId || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'SupplierBatchNo' | abpLocalization }}</small>
                <div>{{ b.supplierBatchNo || '—' }}</div>
              </div>
              <div class="col-md-3">
                <small class="text-muted">{{ 'BatchWiseValuation' | abpLocalization }}</small>
                <div>
                  @if (b.useBatchwiseValuation) {
                    <span class="badge bg-info">{{ 'Enabled' | abpLocalization }}</span>
                  } @else {
                    <span class="badge bg-secondary">{{ 'Disabled' | abpLocalization }}</span>
                  }
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Stock Balance per Warehouse -->
        @if (stockBalance(); as sb) {
          <div class="card mb-3">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fas fa-warehouse me-2"></i>{{ 'StockBalance' | abpLocalization }}</h6>
              <span class="badge bg-primary">{{ 'Total' | abpLocalization }}: {{ sb.totalQuantity | number:'1.2-2' }}</span>
            </div>
            <div class="card-body p-0">
              @if ((sb.warehouseBalances ?? []).length === 0) {
                <div class="text-center text-muted py-3">{{ 'NoStockAvailable' | abpLocalization }}</div>
              } @else {
                <table class="table table-sm table-hover mb-0">
                  <thead><tr class="table-light">
                    <th>{{ 'Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ 'ValuationRate' | abpLocalization }}</th>
                    <th class="text-end">{{ 'StockValue' | abpLocalization }}</th>
                    <th></th>
                  </tr></thead>
                  <tbody>
                    @for (wh of sb.warehouseBalances; track wh.warehouseId) {
                      <tr>
                        <td><i class="fas fa-warehouse text-muted me-1"></i>{{ wh.warehouseName }}</td>
                        <td class="text-end font-monospace fw-bold" [class.text-success]="(wh.quantity ?? 0) > 0" [class.text-danger]="(wh.quantity ?? 0) < 0">{{ wh.quantity | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace">{{ wh.valuationRate | number:'1.2-2' }}</td>
                        <td class="text-end font-monospace">{{ wh.stockValue | number:'1.2-2' }}</td>
                        <td class="text-end">
                          <a class="btn btn-outline-primary btn-sm" [routerLink]="['/inventory/stock-entries/new']"
                             [queryParams]="{ sourceWarehouseId: wh.warehouseId, purpose: 'MaterialTransfer' }">
                            <i class="fas fa-exchange-alt"></i>
                          </a>
                        </td>
                      </tr>
                    }
                  </tbody>
                  <tfoot><tr class="table-light fw-bold">
                    <td>{{ 'Total' | abpLocalization }}</td>
                    <td class="text-end font-monospace">{{ sb.totalQuantity | number:'1.2-2' }}</td>
                    <td></td>
                    <td class="text-end font-monospace">{{ sb.totalValue | number:'1.2-2' }}</td>
                    <td></td>
                  </tr></tfoot>
                </table>
              }
            </div>
          </div>
        }

        <!-- Movement History -->
        @if (movementHistory(); as mh) {
          <div class="card mb-3">
            <div class="card-header">
              <h6 class="mb-0"><i class="fas fa-history me-2"></i>{{ 'MovementHistory' | abpLocalization }}</h6>
            </div>
            <div class="card-body p-0">
              @if ((mh.entries ?? []).length === 0) {
                <div class="text-center text-muted py-3">{{ 'NoMovementsRecorded' | abpLocalization }}</div>
              } @else {
                <table class="table table-sm table-hover mb-0">
                  <thead><tr class="table-light">
                    <th>{{ 'PostingDate' | abpLocalization }}</th>
                    <th>{{ 'Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ 'ValuationRate' | abpLocalization }}</th>
                    <th>{{ 'VoucherType' | abpLocalization }}</th>
                  </tr></thead>
                  <tbody>
                    @for (entry of mh.entries; track entry.id) {
                      <tr>
                        <td>{{ entry.postingDate | date:'dd/MM/yyyy' }}</td>
                        <td>{{ entry.warehouseName }}</td>
                        <td class="text-end font-monospace" [class.text-success]="entry.isInward" [class.text-danger]="!entry.isInward">
                          <i class="fas" [class.fa-arrow-down]="entry.isInward" [class.fa-arrow-up]="!entry.isInward"></i>
                          {{ entry.quantityChange | number:'1.2-2' }}
                        </td>
                        <td class="text-end font-monospace">{{ entry.valuationRate | number:'1.2-2' }}</td>
                        <td><span class="badge bg-light text-dark">{{ entry.voucherType ?? '—' }}</span></td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            </div>
          </div>
        }

        <app-activity-log documentType="Batch" [documentId]="b.id" />
      } @else {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      }
    </div>
  `
})
export class BatchDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private batchService = inject(BatchService);
  private router = inject(Router);

  batch = signal<any>(null);
  stockBalance = signal<BatchStockBalanceDto | null>(null);
  movementHistory = signal<BatchMovementHistoryDto | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.batchService.get(id).subscribe({
      next: (data: any) => {
        this.batch.set(data);
        this.loadStockData(id);
      },
      error: () => this.router.navigate(['/inventory/batches'])
    });
  }

  private loadStockData(batchId: string): void {
    this.batchService.getStockBalance(batchId).subscribe({
      next: (data) => this.stockBalance.set(data),
      error: () => {},
    });
    this.batchService.getMovementHistory(batchId, 50).subscribe({
      next: (data) => this.movementHistory.set(data),
      error: () => {},
    });
  }

  isExpired(): boolean {
    const b = this.batch();
    if (!b?.expiryDate) return false;
    return new Date(b.expiryDate) < new Date();
  }

  daysUntilExpiry(): number | null {
    const b = this.batch();
    if (!b?.expiryDate) return null;
    const diff = new Date(b.expiryDate).getTime() - new Date().getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }

  disable(): void {
    this.confirmation.warn('::DisableConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.batchService.disable(this.batch()?.id!).subscribe({
        next: () => {
          const b = this.batch();
          if (b) this.batch.set({ ...b, disabled: true });
        }
      });
    });
  }
}
