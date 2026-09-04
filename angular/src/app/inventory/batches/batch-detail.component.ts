import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { BatchService } from '../../proxy/inventory/batch.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import type { BatchDto, BatchStockBalanceDto, BatchMovementHistoryDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-batch-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <div class="container-fluid">
      <app-breadcrumb />
      @if (batch(); as b) {
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h5 class="mb-0">
              <i class="fas fa-layer-group me-2"></i>{{ b.batchNo }}
              @if (b.isDisabled) {
                <span class="badge bg-danger ms-2">{{ 'Disabled' | abpLocalization }}</span>
              } @else if (isExpired()) {
                <span class="badge bg-warning text-dark ms-2">{{ 'Expired' | abpLocalization }}</span>
              } @else {
                <span class="badge bg-success ms-2">{{ 'Active' | abpLocalization }}</span>
              }
            </h5>
            @if (!b.isDisabled) {
              <button class="btn btn-outline-danger btn-sm" (click)="disable()">
                <i class="fas fa-ban me-1"></i>{{ 'Disable' | abpLocalization }}
              </button>
            }
          </div>
          <div class="card-body">
            <div class="row mb-4">
              <div class="col-md-3">
                <small class="text-muted">{{ 'Item' | abpLocalization }}</small>
                <div class="fw-bold">{{ b.itemId }}</div>
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
                <div>{{ b.shelfLifeInDays ?? '—' }}</div>
              </div>
            </div>
            <div class="row">
              <div class="col-md-3">
                <small class="text-muted">{{ 'SupplierBatchNo' | abpLocalization }}</small>
                <div>{{ b.supplierBatchNo || '—' }}</div>
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
                          <div class="btn-group">
                            <button class="btn btn-outline-secondary btn-sm" [disabled]="(wh.quantity ?? 0) <= 0"
                              (click)="openSplitDialog(wh.warehouseId!, wh.quantity ?? 0)" title="{{ 'SplitBatch' | abpLocalization }}">
                              <i class="fas fa-code-branch"></i>
                            </button>
                            <button class="btn btn-outline-primary btn-sm" [disabled]="(wh.quantity ?? 0) <= 0"
                              (click)="openMoveDialog(wh.warehouseId!, wh.quantity ?? 0)" title="{{ 'MoveBatch' | abpLocalization }}">
                              <i class="fas fa-exchange-alt"></i>
                            </button>
                          </div>
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

        @if (showSplitDialog()) {
          <div class="card border-secondary mb-3">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fas fa-code-branch me-2"></i>{{ 'SplitBatch' | abpLocalization }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showSplitDialog.set(false)"></button>
            </div>
            <div class="card-body">
              <div class="row g-2">
                <div class="col-md-4">
                  <label class="form-label">{{ 'NewBatchNo' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" [(ngModel)]="splitNewBatchNo" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ 'SplitQuantity' | abpLocalization }} ({{ 'Available' | abpLocalization }}: {{ splitAvailableQty() | number:'1.2-2' }})</label>
                  <input type="number" class="form-control form-control-sm" [(ngModel)]="splitQuantity" min="0" [max]="splitAvailableQty()" step="0.01" />
                </div>
                <div class="col-md-4 d-flex align-items-end">
                  <button class="btn btn-sm btn-secondary" [disabled]="isSplitting() || !splitNewBatchNo || splitQuantity() <= 0" (click)="submitSplit()">
                    @if (isSplitting()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                    {{ 'SplitBatch' | abpLocalization }}
                  </button>
                </div>
              </div>
            </div>
          </div>
        }

        @if (showMoveDialog()) {
          <div class="card border-primary mb-3">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fas fa-exchange-alt me-2"></i>{{ 'MoveBatch' | abpLocalization }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showMoveDialog.set(false)"></button>
            </div>
            <div class="card-body">
              <div class="row g-2">
                <div class="col-md-4">
                  <label class="form-label">{{ 'TargetWarehouse' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="moveTargetWarehouseId">
                    <option value="">-- {{ 'SelectWarehouse' | abpLocalization }} --</option>
                    @for (w of warehouses(); track w.id) {
                      @if (w.id !== moveSourceWarehouseId()) {
                        <option [value]="w.id">{{ w.name }}</option>
                      }
                    }
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ 'MoveQuantity' | abpLocalization }} ({{ 'Available' | abpLocalization }}: {{ moveAvailableQty() | number:'1.2-2' }})</label>
                  <input type="number" class="form-control form-control-sm" [(ngModel)]="moveQuantity" min="0" [max]="moveAvailableQty()" step="0.01" />
                </div>
                <div class="col-md-4 d-flex align-items-end">
                  <button class="btn btn-sm btn-primary" [disabled]="isMoving() || !moveTargetWarehouseId() || moveQuantity() <= 0" (click)="submitMove()">
                    @if (isMoving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                    {{ 'MoveBatch' | abpLocalization }}
                  </button>
                </div>
              </div>
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

        <app-activity-log documentType="Batch" [documentId]="b.id!" />
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
  private warehouseService = inject(WarehouseService);
  private toaster = inject(ToasterService);
  private router = inject(Router);

  batch = signal<BatchDto | null>(null);
  stockBalance = signal<BatchStockBalanceDto | null>(null);
  movementHistory = signal<BatchMovementHistoryDto | null>(null);
  warehouses = signal<{ id: string; name: string }[]>([]);

  showSplitDialog = signal(false);
  splitAvailableQty = signal(0);
  splitNewBatchNo = signal('');
  splitQuantity = signal(0);
  isSplitting = signal(false);
  private splitWarehouseId = '';

  showMoveDialog = signal(false);
  moveAvailableQty = signal(0);
  moveSourceWarehouseId = signal('');
  moveTargetWarehouseId = signal('');
  moveQuantity = signal(0);
  isMoving = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.batchService.get(id).subscribe({
      next: (data: any) => {
        this.batch.set(data);
        this.loadStockData(id);
      },
      error: () => this.router.navigate(['/inventory/batches'])
    });
    this.warehouseService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe({
      next: (r: any) => this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, name: w.name ?? w.id }))),
      error: () => {}
    });
  }

  private stockRequestId = 0;

  private loadStockData(batchId: string): void {
    const requestId = ++this.stockRequestId;
    this.batchService.getStockBalance(batchId).subscribe({
      next: (data) => {
        if (requestId === this.stockRequestId) {
          this.stockBalance.set(data);
        }
      },
      error: () => {},
    });
    this.batchService.getMovementHistory(batchId, 50).subscribe({
      next: (data) => {
        if (requestId === this.stockRequestId) {
          this.movementHistory.set(data);
        }
      },
      error: () => {},
    });
  }

  isExpired(): boolean {
    const b = this.batch();
    if (!b?.expiryDate) return false;
    // Per ERPNext PR #58736 (commit 00f04fc084): show Expired status only after expiry date has passed
    const exp = new Date(b.expiryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    exp.setHours(0, 0, 0, 0);
    return exp.getTime() < today.getTime();
  }

  daysUntilExpiry(): number | null {
    const b = this.batch();
    if (!b?.expiryDate) return null;
    const exp = new Date(b.expiryDate);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    exp.setHours(0, 0, 0, 0);
    const diff = exp.getTime() - today.getTime();
    return Math.round(diff / (1000 * 60 * 60 * 24));
  }

  disable(): void {
    this.confirmation.warn('::DisableConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.batchService.disable(this.batch()?.id!).subscribe({
        next: () => {
          const b = this.batch();
          if (b) this.batch.set({ ...b, isDisabled: true });
        }
      });
    });
  }

  openSplitDialog(warehouseId: string, availableQty: number): void {
    this.splitWarehouseId = warehouseId;
    this.splitAvailableQty.set(availableQty);
    this.splitNewBatchNo.set('');
    this.splitQuantity.set(0);
    this.showMoveDialog.set(false);
    this.showSplitDialog.set(true);
  }

  submitSplit(): void {
    const b = this.batch();
    if (!b?.id) return;
    this.isSplitting.set(true);
    this.batchService.splitBatch({
      sourceBatchId: b.id,
      newBatchNo: this.splitNewBatchNo(),
      warehouseId: this.splitWarehouseId,
      splitQuantity: this.splitQuantity(),
    }).subscribe({
      next: () => {
        this.isSplitting.set(false);
        this.showSplitDialog.set(false);
        this.toaster.success('::SuccessfullySplit');
        this.loadStockData(b.id!);
      },
      error: (err: any) => {
        this.isSplitting.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }

  openMoveDialog(warehouseId: string, availableQty: number): void {
    this.moveSourceWarehouseId.set(warehouseId);
    this.moveAvailableQty.set(availableQty);
    this.moveTargetWarehouseId.set('');
    this.moveQuantity.set(0);
    this.showSplitDialog.set(false);
    this.showMoveDialog.set(true);
  }

  submitMove(): void {
    const b = this.batch();
    if (!b?.id) return;
    this.isMoving.set(true);
    this.batchService.moveBatch({
      batchId: b.id,
      sourceWarehouseId: this.moveSourceWarehouseId(),
      targetWarehouseId: this.moveTargetWarehouseId(),
      quantity: this.moveQuantity(),
    }).subscribe({
      next: () => {
        this.isMoving.set(false);
        this.showMoveDialog.set(false);
        this.toaster.success('::SuccessfullyMoved');
        this.loadStockData(b.id!);
      },
      error: (err: any) => {
        this.isMoving.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }
}
