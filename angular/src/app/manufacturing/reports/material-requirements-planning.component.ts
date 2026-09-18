import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { MaterialRequirementsPlanningService } from '../../proxy/manufacturing/material-requirements-planning.service';
import {
  MaterialRequirementsPlanningReportDto,
  MrpBucketSize,
} from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-material-requirements-planning',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">
          <i class="fa fa-boxes-stacked me-2"></i>{{ 'MaterialRequirementsPlanning' | abpLocalization }}
        </h5>
        @if (reportData(); as data) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv(data)">
            <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filter Controls -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3">
            <label class="form-label">{{ 'From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'BucketSize' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="bucketSize">
              <option [value]="MrpBucketSize.Daily">{{ 'Daily' | abpLocalization }}</option>
              <option [value]="MrpBucketSize.Weekly">{{ 'Weekly' | abpLocalization }}</option>
              <option [value]="MrpBucketSize.Monthly">{{ 'Monthly' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <div class="form-check mb-1">
              <input
                type="checkbox"
                class="form-check-input"
                id="includeSafetyStock"
                [(ngModel)]="includeSafetyStock"
              />
              <label class="form-check-label" for="includeSafetyStock">
                {{ 'IncludeSafetyStock' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ 'Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
          </div>
        } @else if (reportData(); as data) {
          @if (data.rows && data.rows.length > 0) {
            <div class="table-responsive">
              <table class="table table-sm table-bordered table-hover align-middle">
                <thead class="table-light">
                  <tr>
                    <th rowspan="2" class="align-middle">{{ 'Item' | abpLocalization }}</th>
                    <th rowspan="2" class="align-middle text-center">{{ 'UOM' | abpLocalization }}</th>
                    <th rowspan="2" class="align-middle text-end">{{ 'CurrentStock' | abpLocalization }}</th>
                    <th rowspan="2" class="align-middle text-end">{{ 'SafetyStock' | abpLocalization }}</th>
                    @for (b of data.buckets; track b.fromDate) {
                      <th class="text-center" colspan="4">{{ b.label }}</th>
                    }
                  </tr>
                  <tr>
                    @for (b of data.buckets; track b.fromDate) {
                      <th class="text-end text-muted small">{{ 'Demand' | abpLocalization }}</th>
                      <th class="text-end text-muted small">{{ 'Supply' | abpLocalization }}</th>
                      <th class="text-end text-muted small">{{ 'Projected' | abpLocalization }}</th>
                      <th class="text-end text-muted small">{{ 'Planned' | abpLocalization }}</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (row of data.rows; track row.itemId) {
                    <tr>
                      <td>
                        <div class="fw-semibold">{{ row.itemCode }}</div>
                        <small class="text-muted">{{ row.itemName }}</small>
                        @if (row.isRawMaterial) {
                          <span class="badge bg-secondary ms-1">RM</span>
                        } @else {
                          <span class="badge bg-info ms-1">FG</span>
                        }
                      </td>
                      <td class="text-center">{{ row.uom }}</td>
                      <td class="text-end">{{ row.currentStock | number:'1.2-2' }}</td>
                      <td class="text-end">{{ row.safetyStock | number:'1.2-2' }}</td>
                      @for (b of row.buckets; track b.bucketFromDate) {
                        <td class="text-end">{{ b.grossRequirements > 0 ? (b.grossRequirements | number:'1.2-2') : '-' }}</td>
                        <td class="text-end">{{ b.scheduledReceipts > 0 ? (b.scheduledReceipts | number:'1.2-2') : '-' }}</td>
                        <td class="text-end" [class.text-danger]="b.projectedAvailableBalance < row.safetyStock">
                          {{ b.projectedAvailableBalance | number:'1.2-2' }}
                        </td>
                        <td class="text-end fw-bold" [class.text-primary]="b.plannedOrders > 0">
                          {{ b.plannedOrders > 0 ? (b.plannedOrders | number:'1.2-2') : '-' }}
                        </td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <div class="alert alert-info mb-0">
              <i class="fa fa-info-circle me-2"></i>{{ 'NoDataAvailable' | abpLocalization }}
            </div>
          }
        }
      </div>
    </div>
  `
})
export class MaterialRequirementsPlanningComponent implements OnInit {
  private mrpService = inject(MaterialRequirementsPlanningService);
  private companyContext = inject(CompanyContextService);

  MrpBucketSize = MrpBucketSize;

  fromDate: string = '';
  toDate: string = '';
  bucketSize: MrpBucketSize = MrpBucketSize.Monthly;
  includeSafetyStock: boolean = true;

  isLoading = signal(false);
  reportData = signal<MaterialRequirementsPlanningReportDto | null>(null);

  ngOnInit(): void {
    const today = new Date();
    this.fromDate = today.toISOString().substring(0, 10);
    const threeMonthsLater = new Date(today);
    threeMonthsLater.setMonth(today.getMonth() + 3);
    this.toDate = threeMonthsLater.toISOString().substring(0, 10);

    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.mrpService
      .getReport({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        bucketSize: Number(this.bucketSize),
        includeSafetyStock: this.includeSafetyStock,
      })
      .subscribe({
        next: data => {
          this.reportData.set(data);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(data: MaterialRequirementsPlanningReportDto): void {
    if (!data.rows || data.rows.length === 0) return;

    const columns = ['itemCode', 'itemName', 'uom', 'isRm', 'currentStock', 'safetyStock'];
    const bucketCols: string[] = [];
    (data.buckets ?? []).forEach((b, idx) => {
      const p = `b${idx}_${(b.label ?? '').replace(/[^a-zA-Z0-9]/g, '_')}`;
      bucketCols.push(`${p}_Demand`, `${p}_Supply`, `${p}_Projected`, `${p}_Planned`);
    });
    const allCols = [...columns, ...bucketCols];

    const rows = data.rows.map(r => {
      const obj: Record<string, any> = {
        itemCode: r.itemCode ?? '',
        itemName: r.itemName ?? '',
        uom: r.uom ?? '',
        isRm: r.isRawMaterial ? 'Yes' : 'No',
        currentStock: r.currentStock ?? 0,
        safetyStock: r.safetyStock ?? 0,
      };
      (r.buckets ?? []).forEach((b, idx) => {
        const bucketLabel = data.buckets?.[idx]?.label ?? '';
        const p = `b${idx}_${bucketLabel.replace(/[^a-zA-Z0-9]/g, '_')}`;
        obj[`${p}_Demand`] = b.grossRequirements ?? 0;
        obj[`${p}_Supply`] = b.scheduledReceipts ?? 0;
        obj[`${p}_Projected`] = b.projectedAvailableBalance ?? 0;
        obj[`${p}_Planned`] = b.plannedOrders ?? 0;
      });
      return obj;
    });

    exportToCsv(`MRP_Report_${this.fromDate}_to_${this.toDate}.csv`, rows, allCols);
  }
}
