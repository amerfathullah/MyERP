import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { IssuedItemsAgainstWorkOrderService } from '../../proxy/manufacturing/issued-items-against-work-order.service';
import type { IssuedItemsAgainstWorkOrderReportDto, IssuedItemsAgainstWorkOrderRowDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-issued-items-against-work-order',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-dolly me-2 text-primary"></i>{{ '::IssuedItemsAgainstWorkOrder' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::IssuedItemsAgainstWorkOrderDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-2 ms-auto">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Work Orders</div>
                  <div class="fs-4 fw-bold text-primary">{{ d.totalWorkOrders }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-secondary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalIssuedLines' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-secondary">{{ d.totalIssuedLines }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Quantity</div>
                  <div class="fs-4 fw-bold text-info">{{ d.totalQuantity | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Amount</div>
                  <div class="fs-4 fw-bold text-success">{{ d.totalAmount | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Data Grid -->
          <div class="table-responsive">
            <table class="table table-striped table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::WorkOrder' | abpLocalization }}</th>
                  <th>{{ '::IssueDate' | abpLocalization }}</th>
                  <th>Stock Entry</th>
                  <th>Finished Good</th>
                  <th>Issued Item</th>
                  <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                  <th>{{ '::Uom' | abpLocalization }}</th>
                  <th class="text-end">Amount</th>
                  <th>Source Wh</th>
                  <th>Target Wh</th>
                  <th class="text-end">{{ '::ProjectedQtyAtSource' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (!d.rows || d.rows.length === 0) {
                  <tr>
                    <td colspan="11" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track $index) {
                  <tr>
                    <td class="fw-bold">{{ row.workOrderNumber }}</td>
                    <td>{{ row.issueDate | date:'yyyy-MM-dd' }}</td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.stockEntryNumber }}</span>
                    </td>
                    <td>
                      <div><strong>{{ row.finishedGoodCode }}</strong></div>
                      @if (row.finishedGoodName && row.finishedGoodName !== row.finishedGoodCode) {
                        <div class="small text-muted">{{ row.finishedGoodName }}</div>
                      }
                    </td>
                    <td>
                      <div><strong>{{ row.itemCode }}</strong></div>
                      @if (row.itemName && row.itemName !== row.itemCode) {
                        <div class="small text-muted">{{ row.itemName }}</div>
                      }
                    </td>
                    <td class="text-end fw-bold">{{ row.quantity | number:'1.2-2' }}</td>
                    <td>{{ row.uom }}</td>
                    <td class="text-end">{{ row.amount | number:'1.2-2' }}</td>
                    <td>{{ row.sourceWarehouseName || '-' }}</td>
                    <td>{{ row.targetWarehouseName || '-' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="row.projectedQtyAtSource < 0">
                      {{ row.projectedQtyAtSource | number:'1.2-2' }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class IssuedItemsAgainstWorkOrderComponent implements OnInit {
  private service = inject(IssuedItemsAgainstWorkOrderService);
  private companyContext = inject(CompanyContextService);

  fromDate = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  toDate = new Date().toISOString().slice(0, 10);

  data = signal<IssuedItemsAgainstWorkOrderReportDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
      })
      .subscribe({
        next: (res) => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows || d.rows.length === 0) return;

    exportToCsv('issued_items_against_work_order.csv', d.rows, [
      'workOrderNumber',
      'issueDate',
      'stockEntryNumber',
      'finishedGoodCode',
      'itemCode',
      'itemName',
      'quantity',
      'uom',
      'amount',
      'sourceWarehouseName',
      'targetWarehouseName',
      'projectedQtyAtSource',
    ]);
  }
}
