import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { WorkOrderStockReportService } from '../../proxy/manufacturing/work-order-stock-report.service';
import type { WorkOrderStockReportDto, WorkOrderStockReportRowDto } from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-work-order-stock-report',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-boxes-stacked me-2 text-primary"></i>{{ '::WorkOrderStockReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::WorkOrderStockReportDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows!.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-4">
            <div class="form-check mt-3">
              <input
                type="checkbox"
                class="form-check-input"
                id="readyOnly"
                [(ngModel)]="readyToBuildOnly"
              />
              <label class="form-check-label small" for="readyOnly">
                {{ '::ReadyToBuildOnly' | abpLocalization }}
              </label>
            </div>
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
              <div class="card border-success text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::ReadyToBuildCount' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-success">{{ d.readyToBuildCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-danger text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::NotReadyCount' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-danger">{{ d.notReadyCount }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-info text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalBuildableQty' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-info">{{ d.totalBuildableQty | number:'1.2-2' }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Detailed Grid -->
          <div class="table-responsive">
            <table class="table table-striped table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::WorkOrder' | abpLocalization }}</th>
                  <th>{{ '::BOM' | abpLocalization }}</th>
                  <th>{{ '::ProductionItem' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">{{ '::QtyToProduce' | abpLocalization }}</th>
                  <th class="text-center">{{ '::RequiredItemsCount' | abpLocalization }}</th>
                  <th class="text-center">{{ '::InStockItemsCount' | abpLocalization }}</th>
                  <th class="text-end">{{ '::BuildableQty' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                  <th class="text-center" style="width: 50px;"></th>
                </tr>
              </thead>
              <tbody>
                @if (!d.rows || d.rows.length === 0) {
                  <tr>
                    <td colspan="10" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track row.workOrderId) {
                  <tr>
                    <td class="fw-bold">{{ row.workOrderNumber }}</td>
                    <td>
                      <div>{{ row.bomNumber }}</div>
                      @if (row.description) {
                        <div class="small text-muted">{{ row.description }}</div>
                      }
                    </td>
                    <td>
                      <div><strong>{{ row.productionItemCode }}</strong></div>
                      @if (row.productionItemName && row.productionItemName !== row.productionItemCode) {
                        <div class="small text-muted">{{ row.productionItemName }}</div>
                      }
                    </td>
                    <td>{{ row.sourceWarehouseName || '-' }}</td>
                    <td class="text-end">{{ row.qtyToBuild | number:'1.2-2' }}</td>
                    <td class="text-center">{{ row.requiredItemsCount }}</td>
                    <td class="text-center">{{ row.inStockItemsCount }}</td>
                    <td class="text-end fw-bold">{{ row.buildableQty | number:'1.2-2' }}</td>
                    <td class="text-center">
                      @if (row.readyToBuild) {
                        <span class="badge bg-success">{{ '::ReadyToBuild' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-warning text-dark">Shortage</span>
                      }
                    </td>
                    <td class="text-center">
                      <button
                        class="btn btn-sm btn-link p-0 text-decoration-none"
                        (click)="toggleRow(row.workOrderId)"
                        title="Toggle Components"
                      >
                        <i
                          class="fa"
                          [class.fa-chevron-down]="!isExpanded(row.workOrderId)"
                          [class.fa-chevron-up]="isExpanded(row.workOrderId)"
                        ></i>
                      </button>
                    </td>
                  </tr>

                  @if (isExpanded(row.workOrderId) && row.componentDetails && row.componentDetails.length > 0) {
                    <tr class="table-light">
                      <td colspan="10" class="p-3">
                        <div class="bg-white p-3 rounded border">
                          <h6 class="mb-2 text-secondary">
                            <i class="fa fa-list-check me-2"></i>Component Stock Breakdown
                          </h6>
                          <table class="table table-sm table-bordered mb-0">
                            <thead class="table-light">
                              <tr>
                                <th>Item</th>
                                <th class="text-end">Required Qty</th>
                                <th class="text-end">Available Stock</th>
                                <th class="text-end">Buildable Units</th>
                                <th class="text-center">Stock Status</th>
                              </tr>
                            </thead>
                            <tbody>
                              @for (comp of row.componentDetails; track comp.itemId) {
                                <tr>
                                  <td>
                                    <strong>{{ comp.itemCode }}</strong> - {{ comp.itemName }}
                                  </td>
                                  <td class="text-end">{{ comp.requiredQty | number:'1.2-2' }}</td>
                                  <td class="text-end fw-bold" [class.text-danger]="!comp.isInStock">
                                    {{ comp.availableQty | number:'1.2-2' }}
                                  </td>
                                  <td class="text-end">{{ comp.buildableQty | number:'1.2-2' }}</td>
                                  <td class="text-center">
                                    @if (comp.isInStock) {
                                      <span class="badge bg-success-subtle text-success">In Stock</span>
                                    } @else {
                                      <span class="badge bg-danger-subtle text-danger">Insufficient</span>
                                    }
                                  </td>
                                </tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class WorkOrderStockReportComponent implements OnInit {
  private service = inject(WorkOrderStockReportService);
  private companyContext = inject(CompanyContextService);

  readyToBuildOnly = false;
  expandedRows = new Set<string>();

  data = signal<WorkOrderStockReportDto | null>(null);
  isLoading = signal<boolean>(false);

  ngOnInit(): void {
    this.loadData();
  }

  isExpanded(woId: string): boolean {
    return this.expandedRows.has(woId);
  }

  toggleRow(woId: string): void {
    if (this.expandedRows.has(woId)) {
      this.expandedRows.delete(woId);
    } else {
      this.expandedRows.add(woId);
    }
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        readyToBuildOnly: this.readyToBuildOnly,
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

    exportToCsv('work_order_stock_report.csv', d.rows, [
      'workOrderNumber',
      'bomNumber',
      'productionItemCode',
      'productionItemName',
      'sourceWarehouseName',
      'qtyToBuild',
      'requiredItemsCount',
      'inStockItemsCount',
      'buildableQty',
      'readyToBuild',
    ]);
  }
}
