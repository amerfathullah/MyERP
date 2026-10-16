import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SupplierQuotationComparisonService } from '../../../proxy/purchasing/supplier-quotation-comparison.service';
import { RequestForQuotationService } from '../../../proxy/purchasing/request-for-quotation.service';
import type {
  SupplierQuotationComparisonDto,
  ComparisonSupplierDto,
  ComparisonItemDto,
  RfqDto,
} from '../../../proxy/purchasing/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-supplier-quotation-comparison',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-code-compare text-primary me-2"></i>
            {{ '::SupplierQuotationComparison' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SupplierQuotationComparisonDescription' | abpLocalization }}
          </p>
        </div>
        @if (comparison() && comparison()!.suppliers && comparison()!.suppliers!.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::SelectRfq' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedRfqId"
                (ngModelChange)="loadComparison()"
              >
                <option value="">-- {{ '::SelectRfq' | abpLocalization }} --</option>
                @for (rfq of rfqs(); track rfq.id) {
                  <option [value]="rfq.id">
                    {{ rfq.rfqNumber || rfq.id }} ({{ rfq.transactionDate | date:'shortDate' }})
                  </option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Status' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedStatus"
                (ngModelChange)="loadComparison()"
              >
                <option value="">-- {{ '::All' | abpLocalization }} --</option>
                <option value="Draft">{{ '::Draft' | abpLocalization }}</option>
                <option value="Submitted">{{ '::Submitted' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::OrderStatus' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedOrderStatus"
                (ngModelChange)="loadComparison()"
              >
                <option value="">-- {{ '::All' | abpLocalization }} --</option>
                <option value="Not Ordered">{{ '::NotOrdered' | abpLocalization }}</option>
                <option value="Partially Ordered">{{ '::PartiallyOrdered' | abpLocalization }}</option>
                <option value="Ordered">{{ '::Ordered' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadComparison()"
                [disabled]="loading() || !selectedRfqId"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Highlights Summary -->
      @if (comparison() && comparison()!.suppliers && comparison()!.suppliers!.length > 0) {
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::Suppliers' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ comparison()!.suppliers!.length }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::Items' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ comparison()!.items?.length || 0 }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::LowestTotal' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">
                {{ comparison()!.lowestTotalAmount | number:'1.2-2' }}
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Comparison Matrix Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (!selectedRfqId) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-hand-pointer fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::SelectRfq' | abpLocalization }}</p>
            </div>
          } @else if (!comparison() || !comparison()!.suppliers || comparison()!.suppliers!.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoComparisonData' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-bordered align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 220px;">{{ '::Item' | abpLocalization }}</th>
                    @for (supp of comparison()!.suppliers; track supp.quotationId) {
                      <th class="text-center" style="min-width: 170px;">
                        <div class="fw-bold text-dark">{{ supp.supplierName }}</div>
                        <div class="small text-muted mb-1">
                          <a [routerLink]="['/purchasing/supplier-quotations', supp.quotationId]" class="text-decoration-none">
                            {{ supp.quotationNumber || 'SQ' }}
                          </a>
                        </div>
                        <span
                          class="badge"
                          [class.bg-success]="supp.orderStatus === 'Ordered'"
                          [class.bg-warning]="supp.orderStatus === 'Partially Ordered'"
                          [class.bg-secondary]="supp.orderStatus === 'Not Ordered'"
                        >
                          {{ supp.orderStatus }}
                        </span>
                      </th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (item of comparison()!.items; track item.itemId) {
                    <tr>
                      <td class="fw-semibold">
                        <div>{{ item.itemDescription }}</div>
                        <div class="small text-muted">
                          {{ '::LowestPrice' | abpLocalization }}:
                          <span class="text-success fw-bold">{{ item.lowestRate | number:'1.2-2' }}</span>
                        </div>
                      </td>
                      @for (supp of comparison()!.suppliers; track supp.quotationId) {
                        @let price = getPrice(item, supp.quotationId);
                        <td class="text-center" [class.table-success]="price?.isLowestPrice">
                          @if (price && price.isQuoted) {
                            <div class="d-flex align-items-center justify-content-center gap-1">
                              <span class="fw-bold fs-6" [class.text-success]="price.isLowestPrice">
                                {{ price.rate | number:'1.2-2' }}
                              </span>
                              @if (price.isLowestPrice) {
                                <i class="fa fa-check-circle text-success" title="{{ '::LowestPrice' | abpLocalization }}"></i>
                              }
                            </div>
                            <div class="small text-muted">
                              {{ '::Qty' | abpLocalization }}: {{ price.quantity | number:'1.0-2' }} |
                              {{ '::Amt' | abpLocalization }}: {{ price.amount | number:'1.2-2' }}
                            </div>
                            @if (price.leadTimeDays) {
                              <div class="badge bg-light text-secondary mt-1">
                                <i class="fa fa-truck me-1"></i>{{ price.leadTimeDays }} {{ '::Days' | abpLocalization }}
                              </div>
                            }
                          } @else {
                            <span class="badge bg-light text-muted">{{ '::NotQuoted' | abpLocalization }}</span>
                          }
                        </td>
                      }
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light fw-bold">
                  <tr class="table-info">
                    <td>{{ '::GrandTotal' | abpLocalization }}</td>
                    @for (supp of comparison()!.suppliers; track supp.quotationId) {
                      <td
                        class="text-center fs-6"
                        [class.table-success]="supp.grandTotal === comparison()!.lowestTotalAmount"
                      >
                        <div>{{ supp.grandTotal | number:'1.2-2' }} {{ supp.currency }}</div>
                        @if (supp.grandTotal === comparison()!.lowestTotalAmount) {
                          <span class="badge bg-success mt-1">
                            <i class="fa fa-trophy me-1"></i>{{ '::LowestTotal' | abpLocalization }}
                          </span>
                        }
                      </td>
                    }
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .table td, .table th {
      padding: 0.5rem 0.75rem;
    }
  `],
})
export class SupplierQuotationComparisonComponent implements OnInit {
  private comparisonService = inject(SupplierQuotationComparisonService);
  private rfqService = inject(RequestForQuotationService);
  private companyContext = inject(CompanyContextService);

  comparison = signal<SupplierQuotationComparisonDto | null>(null);
  rfqs = signal<RfqDto[]>([]);
  loading = signal(false);

  selectedRfqId = '';
  selectedStatus = '';
  selectedOrderStatus = '';

  ngOnInit(): void {
    this.loadRfqs();
  }

  loadRfqs(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.rfqService
      .getList({
        companyId,
        maxResultCount: 100,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => {
          this.rfqs.set(res.items || []);
          if (res.items && res.items.length > 0) {
            this.selectedRfqId = res.items[0].id!;
            this.loadComparison();
          }
        },
      });
  }

  loadComparison(): void {
    if (!this.selectedRfqId) {
      this.comparison.set(null);
      return;
    }

    this.loading.set(true);

    this.comparisonService
      .getComparisonByRfq(
        this.selectedRfqId,
        this.selectedStatus || undefined,
        this.selectedOrderStatus || undefined
      )
      .subscribe({
        next: (res) => {
          this.comparison.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  getPrice(item: ComparisonItemDto, quotationId: string) {
    return item.supplierPrices?.find((p) => p.quotationId === quotationId);
  }

  exportCsv(): void {
    const c = this.comparison();
    if (!c || !c.suppliers || !c.items || c.suppliers.length === 0) return;

    const headers: string[] = ['Item'];
    for (const s of c.suppliers) {
      const name = s.supplierName || 'Supplier';
      headers.push(`${name} (Rate)`, `${name} (Qty)`, `${name} (Amt)`, `${name} (Lead Time)`);
    }

    const dataRows = c.items.map((item) => {
      const row: Record<string, any> = { Item: item.itemDescription };
      for (const s of c.suppliers!) {
        const name = s.supplierName || 'Supplier';
        const p = this.getPrice(item, s.quotationId!);
        row[`${name} (Rate)`] = p && p.isQuoted ? p.rate : '—';
        row[`${name} (Qty)`] = p && p.isQuoted ? p.quantity : '—';
        row[`${name} (Amt)`] = p && p.isQuoted ? p.amount : '—';
        row[`${name} (Lead Time)`] = p && p.leadTimeDays ? `${p.leadTimeDays} days` : '—';
      }
      return row;
    });

    // Grand total row
    const totalRow: Record<string, any> = { Item: 'Grand Total' };
    for (const s of c.suppliers) {
      const name = s.supplierName || 'Supplier';
      totalRow[`${name} (Rate)`] = '';
      totalRow[`${name} (Qty)`] = '';
      totalRow[`${name} (Amt)`] = s.grandTotal;
      totalRow[`${name} (Lead Time)`] = '';
    }
    dataRows.push(totalRow);

    exportToCsv('Supplier_Quotation_Comparison.csv', dataRows, headers);
  }
}
