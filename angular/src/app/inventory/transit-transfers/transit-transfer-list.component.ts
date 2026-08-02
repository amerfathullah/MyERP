import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface PendingTransfer {
  stockEntryId?: string;
  entryNumber?: string;
  postingDate?: string;
  sourceWarehouseId?: string;
  sourceWarehouseName?: string;
  totalQuantity?: number;
  itemCount?: number;
}

@Component({
  selector: 'app-transit-transfer-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-truck-moving me-2"></i>{{ 'TransitTransfers' | abpLocalization }}</h5>
          <div class="btn-group btn-group-sm">
            <a routerLink="/inventory/stock-entries/new" [queryParams]="{ purpose: 'SendToWarehouse' }"
               class="btn btn-outline-primary">
              <i class="fa fa-paper-plane me-1"></i>{{ '::NewTransfer' | abpLocalization }}
            </a>
            <button class="btn btn-primary" (click)="refreshData()">
              <i class="fa fa-refresh me-1"></i>{{ '::Refresh' | abpLocalization }}
            </button>
          </div>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4">
              <div class="spinner-border spinner-border-sm text-primary"></div>
            </div>
          } @else if (pendingTransfers().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-check-circle fa-3x mb-3 text-success"></i>
              <p class="mb-0">{{ 'NoTransfersInTransit' | abpLocalization }}</p>
              <small>{{ 'AllTransfersCompleted' | abpLocalization }}</small>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::EntryNumber' | abpLocalization }}</th>
                    <th>{{ '::PostingDate' | abpLocalization }}</th>
                    <th>{{ '::SourceWarehouse' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Items' | abpLocalization }}</th>
                    <th class="text-end">{{ '::TotalQty' | abpLocalization }}</th>
                    <th>{{ '::Actions' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (transfer of pendingTransfers(); track transfer.stockEntryId) {
                    <tr [class.table-info]="receivingTransferId === transfer.stockEntryId">
                      <td>
                        <a [routerLink]="['/inventory/stock-entries', transfer.stockEntryId]" class="text-decoration-none">
                          {{ transfer.entryNumber }}
                        </a>
                      </td>
                      <td>{{ transfer.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td>{{ transfer.sourceWarehouseName || '—' }}</td>
                      <td class="text-center">
                        <span class="badge bg-secondary">{{ transfer.itemCount }}</span>
                      </td>
                      <td class="text-end fw-semibold">{{ transfer.totalQuantity | number:'1.0-2' }}</td>
                      <td>
                        @if (receivingTransferId === transfer.stockEntryId) {
                          <button class="btn btn-sm btn-outline-secondary" (click)="cancelReceive()">
                            <i class="fa fa-times"></i>
                          </button>
                        } @else {
                          <button class="btn btn-sm btn-success"
                                  (click)="startReceive(transfer)"
                                  [disabled]="isReceiving()">
                            <i class="fa fa-download me-1"></i>{{ '::Receive' | abpLocalization }}
                          </button>
                        }
                      </td>
                    </tr>
                    @if (receivingTransferId === transfer.stockEntryId) {
                      <tr class="table-light">
                        <td colspan="6">
                          <div class="d-flex align-items-end gap-3 py-2">
                            <div class="flex-grow-1" style="max-width: 300px;">
                              <label class="form-label small mb-1">{{ '::DestinationWarehouse' | abpLocalization }}</label>
                              <select class="form-select form-select-sm" [(ngModel)]="destinationWarehouseId">
                                <option value="">— {{ '::SelectWarehouse' | abpLocalization }} —</option>
                                @for (wh of warehouses(); track wh.id) {
                                  <option [value]="wh.id">{{ wh.name }}</option>
                                }
                              </select>
                            </div>
                            <div>
                              <button class="btn btn-sm btn-success"
                                      [disabled]="!destinationWarehouseId || isReceiving()"
                                      (click)="confirmReceive(transfer)">
                                @if (isReceiving()) {
                                  <span class="spinner-border spinner-border-sm me-1"></span>
                                }
                                <i class="fa fa-check me-1"></i>{{ '::ConfirmReceive' | abpLocalization }}
                              </button>
                            </div>
                          </div>
                        </td>
                      </tr>
                    }
                  }
                </tbody>
              </table>
            </div>
            <div class="text-muted small mt-3">
              <i class="fa fa-info-circle me-1"></i>
              {{ pendingTransfers().length }} {{ 'TransfersAwaitingReceipt' | abpLocalization }}
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .badge { font-size: 0.75rem; }
  `]
})
export class TransitTransferListComponent implements OnInit {
  pendingTransfers = signal<PendingTransfer[]>([]);
  warehouses = signal<{ id: string; name: string }[]>([]);
  isLoading = signal(false);
  isReceiving = signal(false);

  receivingTransferId: string | null = null;
  destinationWarehouseId = '';

  private stockEntryService = inject(StockEntryService);
  private warehouseService = inject(WarehouseService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);

  ngOnInit(): void {
    this.loadWarehouses();
    this.refreshData();
  }

  private loadWarehouses(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (res: any) => this.warehouses.set(
        (res.items ?? []).filter((w: any) => !w.isGroup).map((w: any) => ({ id: w.id, name: w.warehouseName ?? w.name ?? w.id }))
      ),
      error: () => {},
    });
  }

  refreshData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.stockEntryService.getPendingTransitTransfers(companyId).subscribe({
      next: (data) => {
        this.pendingTransfers.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  startReceive(transfer: PendingTransfer): void {
    this.receivingTransferId = transfer.stockEntryId ?? null;
    this.destinationWarehouseId = '';
  }

  cancelReceive(): void {
    this.receivingTransferId = null;
    this.destinationWarehouseId = '';
  }

  confirmReceive(transfer: PendingTransfer): void {
    if (!transfer.stockEntryId || !this.destinationWarehouseId) return;

    this.isReceiving.set(true);
    const today = new Date().toISOString().split('T')[0];

    this.stockEntryService.createReceiveAtWarehouse(
      transfer.stockEntryId,
      this.destinationWarehouseId,
      today
    ).subscribe({
      next: (result) => {
        this.isReceiving.set(false);
        this.receivingTransferId = null;
        this.destinationWarehouseId = '';
        this.toaster.success('::SuccessfullyCreated');
        this.refreshData();
        if (result?.id) {
          this.router.navigate(['/inventory/stock-entries', result.id]);
        }
      },
      error: (err: any) => {
        this.isReceiving.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }
}
