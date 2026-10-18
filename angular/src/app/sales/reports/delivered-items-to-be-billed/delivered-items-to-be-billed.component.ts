import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { DeliveredItemsToBeBilledService } from '../../../proxy/sales/delivered-items-to-be-billed.service';
import type {
  DeliveredItemsToBeBilledReportDto,
  DeliveredItemToBeBilledRowDto,
} from '../../../proxy/sales/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-delivered-items-to-be-billed',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-file-invoice-dollar text-primary me-2"></i>
            {{ '::DeliveredItemsToBeBilled' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::DeliveredItemsToBeBilledDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::PostingDate' | abpLocalization }}</label>
              <input
                type="date"
                class="form-control form-control-sm"
                [(ngModel)]="postingDate"
                (ngModelChange)="loadReport()"
              />
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="Filter by DN, customer, item..."
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md-2">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-rotate me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Summary Cards -->
      @if (report()) {
        <div class="row g-3 mb-3">
          <div class="col-sm-6 col-lg-3">
            <div class="card border-0 shadow-sm bg-primary bg-opacity-10">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::TotalAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-primary">
                  {{ report()!.totalAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
          <div class="col-sm-6 col-lg-3">
            <div class="card border-0 shadow-sm bg-info bg-opacity-10">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::BilledAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-info">
                  {{ report()!.totalBilledAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
          <div class="col-sm-6 col-lg-3">
            <div class="card border-0 shadow-sm bg-warning bg-opacity-10">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::ReturnedAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-warning">
                  {{ report()!.totalReturnedAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
          <div class="col-sm-6 col-lg-3">
            <div class="card border-0 shadow-sm bg-danger bg-opacity-10">
              <div class="card-body p-3">
                <div class="text-muted small mb-1">{{ '::PendingAmount' | abpLocalization }}</div>
                <h5 class="mb-0 fw-bold text-danger">
                  {{ report()!.totalPendingAmount | number: '1.2-2' }} {{ report()!.currency }}
                </h5>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Data Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status"></div>
              <div class="text-muted small mt-2">{{ '::Loading' | abpLocalization }}...</div>
            </div>
          } @else if (!report() || filteredRows().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-folder-open fa-2x mb-2 text-secondary opacity-50"></i>
              <p class="mb-0">{{ '::NoDeliveredItemsToBeBilledFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover align-middle mb-0">
                <thead class="table-light text-muted small text-uppercase">
                  <tr>
                    <th>{{ '::DeliveryNote' | abpLocalization }}</th>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::Customer' | abpLocalization }}</th>
                    <th>{{ '::ItemCode' | abpLocalization }}</th>
                    <th class="text-end">{{ '::DeliveredQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::BilledQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReturnedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PendingQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::BilledAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReturnedAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ '::PendingAmount' | abpLocalization }}</th>
                    <th>{{ '::Project' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody class="small">
                  @for (row of filteredRows(); track row.deliveryNoteId + '-' + row.itemId) {
                    <tr>
                      <td class="fw-semibold text-primary">{{ row.deliveryNumber }}</td>
                      <td>{{ row.postingDate | date: 'mediumDate' }}</td>
                      <td>{{ row.customerName }}</td>
                      <td>
                        <span class="fw-medium">{{ row.itemCode }}</span>
                        @if (row.itemName && row.itemName !== row.itemCode) {
                          <span class="text-muted small d-block">{{ row.itemName }}</span>
                        }
                      </td>
                      <td class="text-end">{{ row.deliveredQty | number: '1.2-2' }}</td>
                      <td class="text-end">{{ row.billedQty | number: '1.2-2' }}</td>
                      <td class="text-end">{{ row.returnedQty | number: '1.2-2' }}</td>
                      <td class="text-end fw-semibold text-warning">{{ row.pendingQty | number: '1.2-2' }}</td>
                      <td class="text-end">{{ row.amount | number: '1.2-2' }}</td>
                      <td class="text-end text-info">{{ row.billedAmount | number: '1.2-2' }}</td>
                      <td class="text-end text-warning">{{ row.returnedAmount | number: '1.2-2' }}</td>
                      <td class="text-end fw-bold text-danger">{{ row.pendingAmount | number: '1.2-2' }}</td>
                      <td class="text-muted">{{ row.projectName || '-' }}</td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold small">
                  <tr>
                    <td colspan="4" class="text-uppercase">{{ '::Total' | abpLocalization }}</td>
                    <td colspan="4"></td>
                    <td class="text-end">{{ report()!.totalAmount | number: '1.2-2' }}</td>
                    <td class="text-end text-info">{{ report()!.totalBilledAmount | number: '1.2-2' }}</td>
                    <td class="text-end text-warning">{{ report()!.totalReturnedAmount | number: '1.2-2' }}</td>
                    <td class="text-end text-danger">{{ report()!.totalPendingAmount | number: '1.2-2' }}</td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class DeliveredItemsToBeBilledComponent implements OnInit {
  private reportService = inject(DeliveredItemsToBeBilledService);
  private companyContext = inject(CompanyContextService);

  report = signal<DeliveredItemsToBeBilledReportDto | null>(null);
  loading = signal<boolean>(false);

  postingDate: string = new Date().toISOString().substring(0, 10);
  searchTerm: string = '';

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.reportService
      .getReport({
        companyId,
        postingDate: this.postingDate ? `${this.postingDate}T23:59:59Z` : null,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  filteredRows(): DeliveredItemToBeBilledRowDto[] {
    const rows = this.report()?.rows ?? [];
    if (!this.searchTerm.trim()) return rows;
    const term = this.searchTerm.toLowerCase();
    return rows.filter(
      (r) =>
        r.deliveryNumber.toLowerCase().includes(term) ||
        r.customerName.toLowerCase().includes(term) ||
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        (r.projectName && r.projectName.toLowerCase().includes(term))
    );
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || rep.rows.length === 0) return;

    const columns: (keyof DeliveredItemToBeBilledRowDto)[] = [
      'deliveryNumber',
      'postingDate',
      'customerName',
      'itemCode',
      'itemName',
      'deliveredQty',
      'billedQty',
      'returnedQty',
      'pendingQty',
      'amount',
      'billedAmount',
      'returnedAmount',
      'pendingAmount',
      'projectName',
      'currency',
    ];

    exportToCsv(`delivered_items_to_be_billed_${this.postingDate}.csv`, this.filteredRows(), columns as string[]);
  }
}
