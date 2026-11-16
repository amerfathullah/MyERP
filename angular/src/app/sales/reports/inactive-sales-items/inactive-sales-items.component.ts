import { Component, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { InactiveSalesItemsService } from '../../../proxy/sales/inactive-sales-items.service';
import type { InactiveSalesItemDto, InactiveSalesItemsReportDto } from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

export function filterInactiveSalesItems(items: InactiveSalesItemDto[], term: string): InactiveSalesItemDto[] {
  const q = term.trim().toLowerCase();
  if (!q) return items;
  return items.filter(
    item =>
      (item.itemCode ?? '').toLowerCase().includes(q) ||
      (item.itemName ?? '').toLowerCase().includes(q) ||
      (item.itemGroup ?? '').toLowerCase().includes(q) ||
      (item.customerName ?? '').toLowerCase().includes(q) ||
      (item.territory ?? '').toLowerCase().includes(q)
  );
}

@Component({
  selector: 'app-inactive-sales-items',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-box-open text-warning me-2"></i>{{ '::InactiveSalesItems' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">{{ '::InactiveSalesItemsDescription' | abpLocalization }}</p>
        </div>
        @if (filteredItems().length > 0) {
          <button type="button" class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::DocType' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="docType" (change)="loadReport()">
                <option value="Sales Order">Sales Order</option>
                <option value="Sales Invoice">Sales Invoice</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::DaysSinceLastOrder' | abpLocalization }}</label>
              <input type="number" min="1" class="form-control form-control-sm"
                [(ngModel)]="daysSinceLastOrder" (change)="loadReport()" />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'::Search' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)" />
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::AsOfDate' | abpLocalization }}</label>
              <input type="date" class="form-control form-control-sm"
                [(ngModel)]="asOfDate" (change)="loadReport()" />
            </div>
            <div class="col-md-3 d-flex align-items-end">
              <button type="button" class="btn btn-primary btn-sm w-100" (click)="loadReport()" [disabled]="isLoading()">
                @if (isLoading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync-alt me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      @if (report()) {
        <div class="card shadow-sm border-0">
          <div class="card-header d-flex justify-content-between align-items-center bg-white">
            <span class="fw-semibold">{{ '::TotalItems' | abpLocalization }}: {{ filteredItems().length }}</span>
          </div>
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::ItemGroup' | abpLocalization }}</th>
                  <th>{{ '::Customer' | abpLocalization }}</th>
                  <th>{{ '::Territory' | abpLocalization }}</th>
                  <th>{{ '::LastOrderDate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DaysSinceLastOrder' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredItems(); track row.itemId ?? row.itemCode) {
                  <tr>
                    <td class="font-monospace small">{{ row.itemCode }}</td>
                    <td class="small">{{ row.itemName }}</td>
                    <td class="small">{{ row.itemGroup || '&mdash;' }}</td>
                    <td class="small">{{ row.customerName || '&mdash;' }}</td>
                    <td class="small">{{ row.territory || '&mdash;' }}</td>
                    <td class="small">{{ row.lastOrderDate ? (row.lastOrderDate | date:'yyyy-MM-dd') : 'Never' }}</td>
                    <td class="text-end font-monospace small">{{ row.quantity !== null && row.quantity !== undefined ? (row.quantity | number:'1.2-2') : '&mdash;' }}</td>
                    <td class="text-end font-monospace small fw-semibold">
                      {{ row.daysSinceLastOrder !== null && row.daysSinceLastOrder !== undefined ? row.daysSinceLastOrder : 'Never' }}
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="8" class="text-center text-muted py-4">{{ '::NoDataAvailable' | abpLocalization }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class InactiveSalesItemsComponent implements OnInit {
  private service = inject(InactiveSalesItemsService);
  private companyContext = inject(CompanyContextService);

  docType = 'Sales Order';
  daysSinceLastOrder = 90;
  searchTerm = signal('');
  asOfDate = new Date().toISOString().substring(0, 10);
  isLoading = signal(false);
  report = signal<InactiveSalesItemsReportDto | null>(null);

  filteredItems = computed(() => filterInactiveSalesItems(this.report()?.items ?? [], this.searchTerm()));

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    this.isLoading.set(true);
    this.service.getReport({
      companyId: companyId || undefined,
      docType: this.docType,
      daysSinceLastOrder: this.daysSinceLastOrder > 0 ? this.daysSinceLastOrder : 90,
      asOfDate: this.asOfDate || undefined,
    }).subscribe({
      next: res => {
        this.report.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const items = this.filteredItems();
    if (items.length === 0) return;
    exportToCsv(
      `Inactive-Sales-Items-${this.docType.replace(/\s+/g, '_')}-${new Date().toISOString().substring(0, 10)}`,
      items.map(r => ({
        ItemCode: r.itemCode ?? '',
        ItemName: r.itemName ?? '',
        ItemGroup: r.itemGroup ?? '',
        Customer: r.customerName ?? '',
        Territory: r.territory ?? '',
        LastOrderDate: r.lastOrderDate ? r.lastOrderDate.substring(0, 10) : '',
        Quantity: r.quantity ?? '',
        DaysSinceLastOrder: r.daysSinceLastOrder ?? '',
      })),
      ['ItemCode', 'ItemName', 'ItemGroup', 'Customer', 'Territory', 'LastOrderDate', 'Quantity', 'DaysSinceLastOrder']
    );
  }
}
