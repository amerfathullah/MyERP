import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import type { ProductionPlanDto, ProductionPlanSummaryDto, ProductionPlanSummaryRowDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-production-plan-summary',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-diagram-project me-2 text-primary"></i>{{ '::ProductionPlanSummary' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ProductionPlanSummaryDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows!.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filter Bar -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-5">
            <label class="form-label small text-muted">Production Plan</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedPlanId" (ngModelChange)="onPlanChange()">
              <option [ngValue]="null">-- Select Production Plan --</option>
              @for (p of planList(); track p.id) {
                <option [ngValue]="p.id">
                  {{ p.planNumber }} ({{ p.status }})
                </option>
              }
            </select>
          </div>
          <div class="col-md-2 ms-auto">
            <button
              class="btn btn-primary btn-sm w-100"
              (click)="loadData()"
              [disabled]="isLoading() || !selectedPlanId"
            >
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (isLoading()) {
          <div class="text-center py-5">
            <i class="fa fa-spinner fa-spin fa-2x text-primary"></i>
          </div>
        } @else if (data(); as d) {
          <!-- KPI Summary Cards -->
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="card border-primary text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Plan Number</div>
                  <div class="fs-5 fw-bold text-primary">{{ d.planNumber }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Planned Qty</div>
                  <div class="fs-4 fw-bold text-info">{{ totalPlannedQty() | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Produced Qty</div>
                  <div class="fs-4 fw-bold text-success">{{ totalProducedQty() | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">Total Pending Qty</div>
                  <div class="fs-4 fw-bold text-warning">{{ totalPendingQty() | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Detailed Tree Grid -->
          <div class="table-responsive">
            <table class="table table-striped table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 250px;">Item / Document</th>
                  <th>Item Name</th>
                  <th>Sales Order</th>
                  <th>Document Type</th>
                  <th>Status</th>
                  <th class="text-center">{{ '::BOMLevel' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OrderQty' | abpLocalization }}</th>
                  <th class="text-end">Produced Qty</th>
                  <th class="text-end">Pending Qty</th>
                </tr>
              </thead>
              <tbody>
                @if (!d.rows || d.rows.length === 0) {
                  <tr>
                    <td colspan="9" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track $index) {
                  <tr [class.table-light]="row.indent === 0">
                    <td>
                      <div class="d-flex align-items-center" [style.padding-left.px]="(row.indent || 0) * 24">
                        @if ((row.indent || 0) > 0) {
                          <i class="fa fa-level-up-alt fa-rotate-90 me-2 text-muted"></i>
                          <i class="fa fa-gears me-2 text-primary"></i>
                          <div>
                            <strong>{{ row.documentName }}</strong>
                            <div class="small text-muted">{{ row.itemCode }}</div>
                          </div>
                        } @else {
                          <i class="fa fa-cube me-2 text-success"></i>
                          <strong>{{ row.itemCode }}</strong>
                        }
                      </div>
                    </td>
                    <td>{{ row.itemName || '-' }}</td>
                    <td>{{ row.salesOrderNumber || '-' }}</td>
                    <td>
                      @if (row.documentType) {
                        <span class="badge bg-light text-dark border">{{ row.documentType }}</span>
                      } @else {
                        <span class="badge bg-secondary-subtle text-secondary">Finished Good</span>
                      }
                    </td>
                    <td>
                      @if (row.status) {
                        <span class="badge bg-info-subtle text-info border">{{ row.status }}</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="text-center">
                      <span class="badge bg-light text-dark border">L{{ row.bomLevel ?? 0 }}</span>
                    </td>
                    <td class="text-end fw-bold">{{ row.qty | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold text-success">{{ row.producedQty | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="(row.pendingQty || 0) > 0">
                      {{ row.pendingQty | number:'1.2-2' }}
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
export class ProductionPlanSummaryComponent implements OnInit {
  private service = inject(ProductionPlanService);
  private companyContext = inject(CompanyContextService);

  planList = signal<ProductionPlanDto[]>([]);
  selectedPlanId: string | null = null;

  data = signal<ProductionPlanSummaryDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    this.loadPlanList();
  }

  loadPlanList(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.service
      .getList({
        companyId,
        maxResultCount: 100,
        skipCount: 0,
      })
      .subscribe({
        next: (res) => {
          const items = res.items || [];
          this.planList.set(items);
          if (items.length > 0 && !this.selectedPlanId) {
            this.selectedPlanId = items[0].id!;
            this.loadData();
          }
        },
      });
  }

  onPlanChange(): void {
    if (this.selectedPlanId) {
      this.loadData();
    }
  }

  loadData(): void {
    if (!this.selectedPlanId) return;

    this.isLoading.set(true);
    this.service.getSummaryReport(this.selectedPlanId).subscribe({
      next: (res) => {
        this.data.set(res);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      },
    });
  }

  totalPlannedQty(): number {
    const d = this.data();
    if (!d || !d.rows) return 0;
    return d.rows
      .filter((r) => r.indent === 0)
      .reduce((sum, r) => sum + (r.qty || 0), 0);
  }

  totalProducedQty(): number {
    const d = this.data();
    if (!d || !d.rows) return 0;
    return d.rows
      .filter((r) => r.indent === 0)
      .reduce((sum, r) => sum + (r.producedQty || 0), 0);
  }

  totalPendingQty(): number {
    const d = this.data();
    if (!d || !d.rows) return 0;
    return d.rows
      .filter((r) => r.indent === 0)
      .reduce((sum, r) => sum + (r.pendingQty || 0), 0);
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows || d.rows.length === 0) return;

    exportToCsv('production_plan_summary.csv', d.rows, [
      'indent',
      'itemCode',
      'itemName',
      'salesOrderNumber',
      'documentType',
      'documentName',
      'status',
      'bomLevel',
      'qty',
      'producedQty',
      'pendingQty',
    ]);
  }
}
