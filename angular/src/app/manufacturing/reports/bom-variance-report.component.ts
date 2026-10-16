import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { BomVarianceReportService } from '../../proxy/manufacturing/bom-variance-report.service';
import { BomVarianceReportDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-bom-variance-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-balance-scale me-2"></i>{{ 'BomVarianceReport' | abpLocalization }}</h5>
        @if (data()) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>
      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4">
          <div class="col-md-3">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-4 d-flex align-items-center pt-3">
            <div class="form-check">
              <input class="form-check-input" type="checkbox" id="onlyOverproduced" [(ngModel)]="onlyOverproduced">
              <label class="form-check-label" for="onlyOverproduced">
                Only Overproduced Work Orders (Produced &gt; Ordered)
              </label>
            </div>
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ 'Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (data(); as d) {
          <!-- KPI Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-3 fw-bold text-primary">{{ d.totalWorkOrders }}</div>
                <small class="text-muted">{{ 'TotalWorkOrders' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-4 fw-bold text-info">
                  {{ d.totalOrderedQty | number:'1.2-2' }} / {{ d.totalProducedQty | number:'1.2-2' }}
                </div>
                <small class="text-muted">Ordered vs Produced FG</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-4 fw-bold text-secondary">
                  {{ d.totalRequiredQty | number:'1.2-2' }} / {{ d.totalConsumedQty | number:'1.2-2' }}
                </div>
                <small class="text-muted">Required vs Consumed RM</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center" [class.border-danger]="d.totalVarianceQty > 0">
                <div class="fs-3 fw-bold" [class.text-danger]="d.totalVarianceQty > 0" [class.text-success]="d.totalVarianceQty <= 0">
                  {{ d.totalVarianceQty > 0 ? '+' : '' }}{{ d.totalVarianceQty | number:'1.2-2' }}
                </div>
                <small class="text-muted">Total Variance</small>
              </div>
            </div>
          </div>

          <!-- Table -->
          <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th>{{ 'WorkOrder' | abpLocalization }}</th>
                  <th>BOM</th>
                  <th>{{ 'FinishedGood' | abpLocalization }}</th>
                  <th class="text-end">{{ 'OrderedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ProducedQty' | abpLocalization }}</th>
                  <th>Raw Material</th>
                  <th class="text-end">{{ 'RequiredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ConsumedQty' | abpLocalization }}</th>
                  <th class="text-end">Variance</th>
                </tr>
              </thead>
              <tbody>
                @for (row of d.rows; track row.workOrderId + '-' + row.rawMaterialItemId) {
                  <tr>
                    <td><strong>{{ row.workOrderNumber }}</strong></td>
                    <td><small class="text-muted">{{ row.bomNumber }}</small></td>
                    <td>
                      <div>{{ row.finishedGoodItemName }}</div>
                      <small class="text-muted">{{ row.finishedGoodItemCode }}</small>
                    </td>
                    <td class="text-end">{{ row.orderedQty | number:'1.2-2' }}</td>
                    <td class="text-end fw-medium">{{ row.producedQty | number:'1.2-2' }}</td>
                    <td>
                      <div>{{ row.rawMaterialItemName }}</div>
                      <small class="text-muted">{{ row.rawMaterialItemCode }}</small>
                    </td>
                    <td class="text-end">{{ row.requiredQty | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.consumedQty | number:'1.2-2' }}</td>
                    <td class="text-end">
                      <span class="badge" [class.bg-danger]="row.varianceQty > 0" [class.bg-success]="row.varianceQty <= 0">
                        {{ row.varianceQty > 0 ? '+' : '' }}{{ row.varianceQty | number:'1.2-2' }}
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="9" class="text-center py-4 text-muted">{{ 'NoDataAvailable' | abpLocalization }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `
})
export class BomVarianceReportComponent implements OnInit {
  private service = inject(BomVarianceReportService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  onlyOverproduced = true;

  isLoading = signal(false);
  data = signal<BomVarianceReportDto | null>(null);

  ngOnInit(): void {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
    this.fromDate = firstDay.toISOString().substring(0, 10);
    this.toDate = now.toISOString().substring(0, 10);
    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service.getReport({
      companyId,
      onlyOverproduced: this.onlyOverproduced,
      fromDate: this.fromDate ? this.fromDate : null,
      toDate: this.toDate ? this.toDate : null,
    }).subscribe({
      next: (res) => {
        this.data.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows.length) return;

    const exportRows = d.rows.map(r => ({
      WorkOrderNumber: r.workOrderNumber,
      BomNumber: r.bomNumber,
      FinishedGoodCode: r.finishedGoodItemCode,
      FinishedGoodName: r.finishedGoodItemName,
      OrderedQty: r.orderedQty,
      ProducedQty: r.producedQty,
      RawMaterialCode: r.rawMaterialItemCode,
      RawMaterialName: r.rawMaterialItemName,
      RequiredQty: r.requiredQty,
      ConsumedQty: r.consumedQty,
      VarianceQty: r.varianceQty,
    }));

    const columns = [
      'WorkOrderNumber',
      'BomNumber',
      'FinishedGoodCode',
      'FinishedGoodName',
      'OrderedQty',
      'ProducedQty',
      'RawMaterialCode',
      'RawMaterialName',
      'RequiredQty',
      'ConsumedQty',
      'VarianceQty',
    ];

    exportToCsv(`bom-variance-report-${this.fromDate}-to-${this.toDate}.csv`, exportRows, columns);
  }
}
