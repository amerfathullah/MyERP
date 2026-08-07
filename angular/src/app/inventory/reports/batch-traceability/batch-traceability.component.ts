import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BatchService } from '../../../proxy/inventory/batch.service';
import type { BatchTraceabilityDto } from '../../../proxy/inventory/models';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-batch-traceability',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::BatchTraceability' | abpLocalization">
      <div class="card mb-3">
        <div class="card-body">
          <div class="row g-3 align-items-end">
            <div class="col-md-6">
              <label class="form-label">{{ '::BatchNumber' | abpLocalization }}</label>
              <div class="input-group">
                <input class="form-control" [(ngModel)]="batchSearch"
                  [placeholder]="'::Placeholder:SearchBatch' | abpLocalization"
                  (keyup.enter)="searchBatch()">
                <button class="btn btn-primary" (click)="searchBatch()" [disabled]="isLoading()">
                  @if (isLoading()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fa fa-search me-1"></i>{{ '::Trace' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      @if (traceData(); as data) {
        <!-- KPI Cards -->
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-start border-primary border-4">
              <div class="card-body text-center py-2">
                <small class="text-muted">{{ '::TotalProduced' | abpLocalization }}</small>
                <div class="fw-bold fs-5">{{ data.totalProduced | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-success border-4">
              <div class="card-body text-center py-2">
                <small class="text-muted">{{ '::TotalDelivered' | abpLocalization }}</small>
                <div class="fw-bold fs-5 text-success">{{ data.totalDelivered | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-info border-4">
              <div class="card-body text-center py-2">
                <small class="text-muted">{{ '::CustomersReached' | abpLocalization }}</small>
                <div class="fw-bold fs-5">{{ data.customerCount }}</div>
              </div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-start border-warning border-4">
              <div class="card-body text-center py-2">
                <small class="text-muted">{{ '::RemainingStock' | abpLocalization }}</small>
                <div class="fw-bold fs-5">{{ (data.totalProduced ?? 0) - (data.totalDelivered ?? 0) | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Batch Info -->
        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <span class="fw-bold"><i class="fa fa-barcode me-2"></i>{{ data.batchNo }}</span>
            <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          </div>
          <div class="card-body py-2">
            <div class="row">
              @if (data.manufacturingDate) {
                <div class="col-auto"><small class="text-muted">{{ '::ManufacturingDate' | abpLocalization }}:</small> {{ data.manufacturingDate | date:'mediumDate' }}</div>
              }
              @if (data.expiryDate) {
                <div class="col-auto"><small class="text-muted">{{ '::ExpiryDate' | abpLocalization }}:</small>
                  <span [class.text-danger]="isExpired(data.expiryDate)">{{ data.expiryDate | date:'mediumDate' }}</span>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Customer Summary -->
        @if (data.customerSummary?.length) {
          <div class="card mb-3">
            <div class="card-header fw-bold">
              <i class="fa fa-users me-2"></i>{{ '::AffectedCustomers' | abpLocalization }}
              <span class="badge bg-primary ms-2">{{ data.customerSummary.length }}</span>
            </div>
            <div class="card-body p-0">
              <div class="table-responsive">
                <table class="table table-hover table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::Customer' | abpLocalization }}</th>
                      <th class="text-end">{{ '::TotalQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Deliveries' | abpLocalization }}</th>
                      <th>{{ '::FirstDelivery' | abpLocalization }}</th>
                      <th>{{ '::LastDelivery' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (c of data.customerSummary; track c.customerId) {
                      <tr>
                        <td><a [routerLink]="['/customers', c.customerId]" class="text-decoration-none">{{ c.customerName }}</a></td>
                        <td class="text-end font-monospace">{{ c.totalQuantity | number:'1.2-2' }}</td>
                        <td class="text-end">{{ c.deliveryCount }}</td>
                        <td>{{ c.firstDeliveryDate | date:'mediumDate' }}</td>
                        <td>{{ c.lastDeliveryDate | date:'mediumDate' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        <!-- Delivery Detail -->
        @if (data.deliveries?.length) {
          <div class="card mb-3">
            <div class="card-header fw-bold">
              <i class="fa fa-truck me-2"></i>{{ '::DeliveryHistory' | abpLocalization }}
              <span class="badge bg-secondary ms-2">{{ data.deliveries.length }}</span>
            </div>
            <div class="card-body p-0">
              <div class="table-responsive">
                <table class="table table-hover table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::DeliveryNote' | abpLocalization }}</th>
                      <th>{{ '::Date' | abpLocalization }}</th>
                      <th>{{ '::Customer' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (d of data.deliveries; track d.deliveryNoteId) {
                      <tr>
                        <td><a [routerLink]="['/sales/delivery-notes', d.deliveryNoteId]" class="text-decoration-none">{{ d.deliveryNumber || '—' }}</a></td>
                        <td>{{ d.deliveryDate | date:'mediumDate' }}</td>
                        <td>{{ d.customerName }}</td>
                        <td class="text-end font-monospace">{{ d.quantityDelivered | number:'1.2-2' }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        }

        @if (!data.deliveries?.length) {
          <div class="alert alert-info">
            <i class="fa fa-info-circle me-2"></i>{{ '::NoBatchDeliveries' | abpLocalization }}
          </div>
        }
      }
    </abp-page>
  `,
})
export class BatchTraceabilityComponent {
  private batchService = inject(BatchService);
  private toaster = inject(ToasterService);

  batchSearch = '';
  isLoading = signal(false);
  traceData = signal<BatchTraceabilityDto | null>(null);
  batches = signal<any[]>([]);

  searchBatch(): void {
    if (!this.batchSearch.trim()) return;
    this.isLoading.set(true);
    // First find batch by batch number
    this.batchService.getList({ filter: this.batchSearch.trim(), skipCount: 0, maxResultCount: 1 } as any).subscribe({
      next: (res) => {
        if (res.items?.length) {
          this.loadTraceability(res.items[0].id!);
        } else {
          this.isLoading.set(false);
          this.traceData.set(null);
          this.toaster.warn('::BatchNotFound');
        }
      },
      error: () => { this.isLoading.set(false); },
    });
  }

  private loadTraceability(batchId: string): void {
    this.batchService.getTraceability(batchId).subscribe({
      next: data => {
        this.traceData.set(data);
        this.isLoading.set(false);
      },
      error: () => { this.isLoading.set(false); this.toaster.error('::FailedToLoad'); },
    });
  }

  isExpired(dateStr: string | null | undefined): boolean {
    if (!dateStr) return false;
    return new Date(dateStr) < new Date();
  }

  exportCsv(): void {
    const data = this.traceData();
    if (!data?.deliveries?.length) return;
    const rows = data.deliveries.map(d => ({
      deliveryNumber: d.deliveryNumber ?? '',
      date: d.deliveryDate ?? '',
      customer: d.customerName ?? '',
      quantity: d.quantityDelivered ?? 0,
    }));
    exportToCsv(`batch-traceability-${data.batchNo}.csv`, rows, ['deliveryNumber', 'date', 'customer', 'quantity']);
  }
}
