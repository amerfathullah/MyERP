import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MaterialRequirementsPlanningService } from '../../proxy/manufacturing/material-requirements-planning.service';
import {
  MaterialRequirementsPlanningReportDto,
  MrpBucketSize,
  MrpPlannedOrderRequirementDto,
  MrpOrderRowInputDto,
} from '../../proxy/manufacturing/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-material-requirements-planning',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center flex-wrap gap-2">
        <div class="d-flex align-items-center gap-3">
          <h5 class="mb-0">
            <i class="fa fa-boxes-stacked me-2"></i>{{ 'MaterialRequirementsPlanning' | abpLocalization }}
          </h5>
          <div class="btn-group btn-group-sm" role="group">
            <button
              type="button"
              class="btn"
              [class.btn-primary]="activeTab === 'matrix'"
              [class.btn-outline-primary]="activeTab !== 'matrix'"
              (click)="activeTab = 'matrix'"
            >
              <i class="fa fa-table me-1"></i>{{ 'BucketMatrix' | abpLocalization }}
            </button>
            <button
              type="button"
              class="btn"
              [class.btn-primary]="activeTab === 'orders'"
              [class.btn-outline-primary]="activeTab !== 'orders'"
              (click)="activeTab = 'orders'"
            >
              <i class="fa fa-clipboard-list me-1"></i>{{ 'PlannedOrders' | abpLocalization }}
              @if (reportData()?.requirements?.length; as count) {
                <span class="badge bg-secondary ms-1">{{ count }}</span>
              }
            </button>
          </div>
        </div>

        <div class="d-flex align-items-center gap-2">
          @if (activeTab === 'orders' && selectedRequirementIndices.size > 0) {
            <button
              class="btn btn-sm btn-success"
              [disabled]="isCreatingOrders()"
              (click)="createOrders()"
            >
              <i class="fa fa-file-signature me-1" [class.fa-spin]="isCreatingOrders()"></i>
              {{ 'Make Purchase / Work Order' }} ({{ selectedRequirementIndices.size }})
            </button>
          }
          @if (reportData(); as data) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv(data)">
              <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
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
          <!-- TAB 1: Bucket Matrix -->
          @if (activeTab === 'matrix') {
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

          <!-- TAB 2: Planned Orders / Requirements -->
          @if (activeTab === 'orders') {
            @if (data.requirements && data.requirements.length > 0) {
              <div class="table-responsive">
                <table class="table table-sm table-bordered table-hover align-middle">
                  <thead class="table-light">
                    <tr>
                      <th class="text-center" style="width: 40px">
                        <input
                          type="checkbox"
                          class="form-check-input"
                          [checked]="isAllSelected(data.requirements)"
                          (change)="toggleSelectAll(data.requirements)"
                        />
                      </th>
                      <th>{{ 'Item' | abpLocalization }}</th>
                      <th class="text-center">{{ 'Type' | abpLocalization }}</th>
                      <th>{{ 'BOM' | abpLocalization }}</th>
                      <th class="text-end">{{ 'RequiredQty' | abpLocalization }}</th>
                      <th class="text-center">{{ 'LeadTimeDays' | abpLocalization }}</th>
                      <th class="text-center">{{ 'ReleaseDate' | abpLocalization }}</th>
                      <th class="text-center">{{ 'DeliveryDate' | abpLocalization }}</th>
                      <th>{{ 'DefaultSupplier' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (req of data.requirements; track $index) {
                      <tr [class.table-active]="selectedRequirementIndices.has($index)">
                        <td class="text-center">
                          <input
                            type="checkbox"
                            class="form-check-input"
                            [checked]="selectedRequirementIndices.has($index)"
                            (change)="toggleSelect($index)"
                          />
                        </td>
                        <td>
                          <div class="fw-semibold">{{ req.itemCode }}</div>
                          <small class="text-muted">{{ req.itemName }} ({{ req.uom }})</small>
                        </td>
                        <td class="text-center">
                          @if (req.typeOfMaterial === 'Manufacture') {
                            <span class="badge bg-success">Manufacture</span>
                          } @else {
                            <span class="badge bg-primary">Purchase</span>
                          }
                        </td>
                        <td>
                          @if (req.typeOfMaterial === 'Manufacture') {
                            @if (req.bomNo) {
                              <span class="badge bg-light text-dark border">{{ req.bomNo }}</span>
                            } @else {
                              <span class="badge bg-danger">Missing BOM</span>
                            }
                          } @else {
                            <span class="text-muted small">-</span>
                          }
                        </td>
                        <td class="text-end fw-bold">{{ req.requiredQty | number:'1.2-2' }}</td>
                        <td class="text-center">{{ req.leadTimeDays }}d</td>
                        <td class="text-center">
                          <span class="badge bg-secondary">{{ req.releaseDate | date:'yyyy-MM-dd' }}</span>
                        </td>
                        <td class="text-center">{{ req.deliveryDate | date:'yyyy-MM-dd' }}</td>
                        <td>
                          @if (req.defaultSupplierName) {
                            <span>{{ req.defaultSupplierName }}</span>
                          } @else {
                            <span class="text-muted small">-</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else {
              <div class="alert alert-info mb-0">
                <i class="fa fa-info-circle me-2"></i>{{ 'No planned orders required in this time range.' }}
              </div>
            }
          }
        }
      </div>
    </div>
  `
})
export class MaterialRequirementsPlanningComponent implements OnInit {
  private mrpService = inject(MaterialRequirementsPlanningService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  MrpBucketSize = MrpBucketSize;

  activeTab: 'matrix' | 'orders' = 'matrix';
  fromDate: string = '';
  toDate: string = '';
  bucketSize: MrpBucketSize = MrpBucketSize.Monthly;
  includeSafetyStock: boolean = true;

  isLoading = signal(false);
  isCreatingOrders = signal(false);
  reportData = signal<MaterialRequirementsPlanningReportDto | null>(null);

  selectedRequirementIndices = new Set<number>();

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
    this.selectedRequirementIndices.clear();

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

  isAllSelected(requirements: MrpPlannedOrderRequirementDto[]): boolean {
    return requirements.length > 0 && this.selectedRequirementIndices.size === requirements.length;
  }

  toggleSelectAll(requirements: MrpPlannedOrderRequirementDto[]): void {
    if (this.isAllSelected(requirements)) {
      this.selectedRequirementIndices.clear();
    } else {
      requirements.forEach((_, idx) => this.selectedRequirementIndices.add(idx));
    }
  }

  toggleSelect(index: number): void {
    if (this.selectedRequirementIndices.has(index)) {
      this.selectedRequirementIndices.delete(index);
    } else {
      this.selectedRequirementIndices.add(index);
    }
  }

  createOrders(): void {
    const data = this.reportData();
    const companyId = this.companyContext.selectedCompanyId();
    if (!data?.requirements || !companyId || this.selectedRequirementIndices.size === 0) {
      return;
    }

    const selectedList = Array.from(this.selectedRequirementIndices)
      .map(idx => data.requirements![idx])
      .filter((req): req is MrpPlannedOrderRequirementDto => Boolean(req));

    // PR #58510 & PR #58511: prompt/throw when manufactured item has no BOM
    const missingBomItems = selectedList
      .filter(r => r.typeOfMaterial === 'Manufacture' && !r.bomId)
      .map(r => r.itemCode ?? 'Unknown');

    if (missingBomItems.length > 0) {
      this.toaster.error(
        `Default BOM for ${missingBomItems.join(', ')} not found. Please assign a BOM before creating Work Orders.`
      );
      return;
    }

    const rows: MrpOrderRowInputDto[] = selectedList.map(r => ({
      itemId: r.itemId!,
      itemCode: r.itemCode!,
      itemName: r.itemName,
      typeOfMaterial: r.typeOfMaterial ?? 'Purchase',
      bomId: r.bomId,
      quantity: r.requiredQty ?? 0,
      deliveryDate: r.deliveryDate ?? this.toDate,
      releaseDate: r.releaseDate,
      defaultSupplierId: r.defaultSupplierId,
      warehouseId: r.warehouseId,
    }));

    this.isCreatingOrders.set(true);
    this.mrpService
      .createOrders({
        companyId,
        selectedRows: rows,
      })
      .subscribe({
        next: res => {
          this.isCreatingOrders.set(false);
          this.toaster.success(
            res.message ||
              `Created ${res.purchaseOrdersCount ?? 0} Purchase Order(s) and ${res.workOrdersCount ?? 0} Work Order(s).`
          );
          this.selectedRequirementIndices.clear();
          this.loadData();
        },
        error: () => this.isCreatingOrders.set(false),
      });
  }

  exportCsv(data: MaterialRequirementsPlanningReportDto): void {
    if (this.activeTab === 'orders' && data.requirements && data.requirements.length > 0) {
      const orderCols = [
        'itemCode',
        'itemName',
        'uom',
        'typeOfMaterial',
        'bomNo',
        'requiredQty',
        'leadTimeDays',
        'releaseDate',
        'deliveryDate',
        'defaultSupplierName',
      ];
      const rows = data.requirements.map(r => ({
        itemCode: r.itemCode ?? '',
        itemName: r.itemName ?? '',
        uom: r.uom ?? '',
        typeOfMaterial: r.typeOfMaterial ?? '',
        bomNo: r.bomNo ?? '',
        requiredQty: r.requiredQty ?? 0,
        leadTimeDays: r.leadTimeDays ?? 0,
        releaseDate: r.releaseDate ? r.releaseDate.substring(0, 10) : '',
        deliveryDate: r.deliveryDate ? r.deliveryDate.substring(0, 10) : '',
        defaultSupplierName: r.defaultSupplierName ?? '',
      }));
      exportToCsv(`MRP_Planned_Orders_${this.fromDate}_to_${this.toDate}.csv`, rows, orderCols);
      return;
    }

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
