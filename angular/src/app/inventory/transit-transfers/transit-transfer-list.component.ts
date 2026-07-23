import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
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
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-truck-moving me-2"></i>{{ 'TransitTransfers' | abpLocalization }}</h5>
          <div class="btn-group btn-group-sm">
            <button class="btn btn-primary" (click)="refreshData()">
              <i class="fa fa-refresh me-1"></i>{{ 'Refresh' | abpLocalization }}
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
              <table class="table table-hover">
                <thead>
                  <tr>
                    <th>{{ 'EntryNumber' | abpLocalization }}</th>
                    <th>{{ 'PostingDate' | abpLocalization }}</th>
                    <th>{{ 'SourceWarehouse' | abpLocalization }}</th>
                    <th>{{ 'Items' | abpLocalization }}</th>
                    <th>{{ 'TotalQty' | abpLocalization }}</th>
                    <th>{{ 'Actions' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (transfer of pendingTransfers(); track transfer.stockEntryId) {
                    <tr>
                      <td>
                        <a [routerLink]="['/inventory/stock-entries', transfer.stockEntryId]" class="text-primary">
                          {{ transfer.entryNumber }}
                        </a>
                      </td>
                      <td>{{ transfer.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td>{{ transfer.sourceWarehouseName || transfer.sourceWarehouseId }}</td>
                      <td>
                        <span class="badge bg-secondary">{{ transfer.itemCount }}</span>
                      </td>
                      <td>
                        <span class="fw-semibold">{{ transfer.totalQuantity | number:'1.0-2' }}</span>
                      </td>
                      <td>
                        <button class="btn btn-sm btn-outline-success"
                                (click)="receiveTransfer(transfer)"
                                [disabled]="isReceiving()">
                          <i class="fa fa-download me-1"></i>{{ 'Receive' | abpLocalization }}
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="text-muted small mt-2">
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
  isLoading = signal(false);
  isReceiving = signal(false);

  private stockEntryService = inject(StockEntryService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  ngOnInit(): void {
    this.refreshData();
  }

  refreshData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.stockEntryService.getPendingTransitTransfers(
      `/api/app/stock-entry/pending-transit-transfers?companyId=${companyId}`
    ).subscribe({
      next: (data) => {
        this.pendingTransfers.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  receiveTransfer(transfer: PendingTransfer): void {
    this.isReceiving.set(true);
    // Navigate to stock entry form with pre-filled transit receive context
    window.location.href = `/inventory/stock-entries/new?purpose=ReceiveAtWarehouse&outgoingEntryId=${transfer.stockEntryId}`;
  }
}
