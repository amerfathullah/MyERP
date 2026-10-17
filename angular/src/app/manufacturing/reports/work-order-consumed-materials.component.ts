import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { WorkOrderConsumedMaterialsService } from '../../proxy/manufacturing/work-order-consumed-materials.service';
import type { WorkOrderConsumedMaterialsReportDto } from '../../proxy/manufacturing/models';
import { WorkOrderStatus, workOrderStatusOptions } from '../../proxy/manufacturing/work-order-status.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-work-order-consumed-materials',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-boxes-packing me-2 text-primary"></i>{{ '::WorkOrderConsumedMaterials' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::WorkOrderConsumedMaterialsDescription' | abpLocalization }}</small>
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
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label small text-muted">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-2">
            <label class="form-label small text-muted">{{ '::Status' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedStatus">
              <option [ngValue]="null">-- {{ '::All' | abpLocalization }} --</option>
              @for (opt of statusOptions; track opt.value) {
                <option [ngValue]="opt.value">{{ opt.key }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <div class="form-check mt-3">
              <input
                type="checkbox"
                class="form-check-input"
                id="showExtraOnly"
                [(ngModel)]="showExtraConsumedMaterialsOnly"
              />
              <label class="form-check-label small" for="showExtraOnly">
                {{ '::ShowExtraConsumedMaterials' | abpLocalization }}
              </label>
            </div>
          </div>
          <div class="col-md-2">
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
                  <div class="text-muted small">Raw Material Lines</div>
                  <div class="fs-4 fw-bold text-secondary">{{ d.totalRawMaterialLines }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-danger text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalExtraConsumedItems' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-danger">{{ d.totalExtraConsumedItems }}</div>
                </div>
              </div>
            </div>
            <div class="col-md-3">
              <div class="card border-warning text-center h-100 bg-light">
                <div class="card-body py-3">
                  <div class="text-muted small">{{ '::TotalExtraConsumedQty' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-warning">{{ d.totalExtraConsumedQty | number:'1.2-2' }}</div>
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
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::ProductionItem' | abpLocalization }}</th>
                  <th class="text-end">{{ '::QtyToProduce' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ProducedQty' | abpLocalization }}</th>
                  <th>{{ '::RawMaterial' | abpLocalization }}</th>
                  <th>{{ '::Uom' | abpLocalization }}</th>
                  <th class="text-end">{{ '::RequiredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TransferredQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ConsumedQty' | abpLocalization }}</th>
                  <th class="text-end text-danger">{{ '::ExtraConsumedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalReturnedQty' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @if (!d.rows || d.rows.length === 0) {
                  <tr>
                    <td colspan="12" class="text-center text-muted py-4">
                      {{ '::NoDataAvailable' | abpLocalization }}
                    </td>
                  </tr>
                }
                @for (row of d.rows; track $index) {
                  <tr>
                    <td class="fw-bold">{{ row.workOrderNumber }}</td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.status }}</span>
                    </td>
                    <td>
                      <div><strong>{{ row.productionItemCode }}</strong></div>
                      @if (row.productionItemName && row.productionItemName !== row.productionItemCode) {
                        <div class="small text-muted">{{ row.productionItemName }}</div>
                      }
                    </td>
                    <td class="text-end">{{ row.qtyToProduce | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ row.producedQty | number:'1.2-2' }}</td>
                    <td>
                      <div [class.text-danger]="row.extraConsumedQty > 0">
                        <strong>{{ row.rawMaterialItemCode }}</strong>
                        @if (row.isAdditionalItem) {
                          <span class="badge bg-warning text-dark ms-1">Extra</span>
                        }
                      </div>
                      @if (row.rawMaterialName && row.rawMaterialName !== row.rawMaterialItemCode) {
                        <div class="small text-muted" [class.text-danger]="row.extraConsumedQty > 0">
                          {{ row.rawMaterialName }}
                        </div>
                      }
                    </td>
                    <td>{{ row.stockUom }}</td>
                    <td class="text-end">{{ row.requiredQty | number:'1.2-2' }}</td>
                    <td class="text-end">{{ row.transferredQty | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold">{{ row.consumedQty | number:'1.2-2' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="row.extraConsumedQty > 0">
                      {{ row.extraConsumedQty | number:'1.2-2' }}
                    </td>
                    <td class="text-end text-muted">{{ row.returnedQty | number:'1.2-2' }}</td>
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
export class WorkOrderConsumedMaterialsComponent implements OnInit {
  private service = inject(WorkOrderConsumedMaterialsService);
  private companyContext = inject(CompanyContextService);

  statusOptions = workOrderStatusOptions;

  fromDate = '';
  toDate = '';
  selectedStatus: number | null = null;
  showExtraConsumedMaterialsOnly = false;

  isLoading = signal(false);
  data = signal<WorkOrderConsumedMaterialsReportDto | null>(null);

  ngOnInit(): void {
    const today = new Date();
    const oneMonthAgo = new Date();
    oneMonthAgo.setMonth(oneMonthAgo.getMonth() - 1);

    this.fromDate = oneMonthAgo.toISOString().substring(0, 10);
    this.toDate = today.toISOString().substring(0, 10);

    this.loadData();
  }

  loadData(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.service
      .getReport({
        companyId,
        fromDate: this.fromDate ? this.fromDate : null,
        toDate: this.toDate ? this.toDate : null,
        status: this.selectedStatus,
        showExtraConsumedMaterialsOnly: this.showExtraConsumedMaterialsOnly,
      })
      .subscribe({
        next: res => {
          this.data.set(res);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const d = this.data();
    if (!d || !d.rows || !d.rows.length) return;

    exportToCsv('work_order_consumed_materials.csv', d.rows, [
      'workOrderNumber',
      'status',
      'productionItemCode',
      'productionItemName',
      'qtyToProduce',
      'producedQty',
      'rawMaterialItemCode',
      'rawMaterialName',
      'stockUom',
      'requiredQty',
      'transferredQty',
      'consumedQty',
      'extraConsumedQty',
      'returnedQty',
    ]);
  }
}
