import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SerialNoAndBatchTraceabilityService } from '../../../proxy/inventory/serial-no-and-batch-traceability.service';
import type {
  SerialNoAndBatchTraceabilityReportDto,
  SerialNoAndBatchTraceabilityNodeDto,
  ItemDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-serial-no-and-batch-traceability',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-sitemap me-2 text-primary"></i>
            {{ '::SerialNoAndBatchTraceability' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::SerialNoAndBatchTraceabilityDescription' | abpLocalization }}
          </small>
        </div>
        <div>
          @if (filteredNodes().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters Row 1 -->
        <div class="row g-2 mb-3 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of items(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::SerialNo' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              placeholder="e.g. SN-001..."
              [(ngModel)]="selectedSerialNo"
              (keyup.enter)="loadData()"
            />
          </div>

          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::BatchNumber' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              placeholder="e.g. BATCH-001..."
              [(ngModel)]="selectedBatchNo"
              (keyup.enter)="loadData()"
            />
          </div>

          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::TraceabilityDirection' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedDirection">
              <option value="Both">Complete Genealogy (Up & Down)</option>
              <option value="Backward">{{ '::Backward' | abpLocalization }}</option>
              <option value="Forward">{{ '::Forward' | abpLocalization }}</option>
            </select>
          </div>

          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Trace' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filters Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6 col-sm-12">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search within trace results (item, voucher, supplier, customer)...'"
              [(ngModel)]="searchTerm"
            />
          </div>
        </div>

        <!-- KPI Metrics -->
        @if (reportData(); as data) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Total Trace Nodes</small>
                <span class="fs-4 fw-bold text-primary">{{ data.totalNodes }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Root Starting Nodes</small>
                <span class="fs-4 fw-bold text-dark">{{ data.rootNodesCount }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Suppliers Linked</small>
                <span class="fs-4 fw-bold text-info">{{ supplierCount() }}</span>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Customers Reached</small>
                <span class="fs-4 fw-bold text-success">{{ customerCount() }}</span>
              </div>
            </div>
          </div>
        }

        <!-- Results Tree Table -->
        <div class="table-responsive border rounded">
          <table class="table table-sm table-hover align-middle mb-0">
            <thead class="table-light text-nowrap">
              <tr>
                <th style="min-width: 280px;">Item / Genealogy Node</th>
                <th>Serial / Batch #</th>
                <th>Direction</th>
                <th>Voucher / Transaction</th>
                <th>Work Order</th>
                <th>Warehouse</th>
                <th class="text-end">Qty</th>
                <th>Posting Date</th>
                <th>Supplier / Customer</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    <i class="fa fa-spinner fa-spin me-2"></i>Loading traceability genealogy...
                  </td>
                </tr>
              } @else if (filteredNodes().length === 0) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    <i class="fa fa-info-circle me-1"></i>
                    {{ '::NoTraceabilityRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (node of filteredNodes(); track node.nodeId) {
                  <tr [class.table-active]="node.indent === 0">
                    <td>
                      <div class="d-flex align-items-center" [style.paddingLeft.px]="node.indent * 24">
                        @if (node.indent > 0) {
                          <span class="text-muted font-monospace me-2">└──</span>
                        }
                        @if (node.indent === 0) {
                          <span class="badge bg-secondary me-2">Root</span>
                        }
                        <div>
                          <span class="fw-semibold">{{ node.itemCode }}</span>
                          <div class="text-muted small text-truncate" style="max-width: 220px;">
                            {{ node.itemName }}
                          </div>
                        </div>
                      </div>
                    </td>
                    <td>
                      @if (node.serialNo) {
                        <div><i class="fa fa-tag me-1 text-primary small"></i><strong>SN:</strong> {{ node.serialNo }}</div>
                      }
                      @if (node.batchNo) {
                        <div><i class="fa fa-boxes-stacked me-1 text-success small"></i><strong>Batch:</strong> {{ node.batchNo }}</div>
                        @if (node.batchExpiryDate) {
                          <small class="text-muted">Exp: {{ node.batchExpiryDate | date:'shortDate' }}</small>
                        }
                      }
                    </td>
                    <td>
                      @if (node.direction === 'Backward') {
                        <span class="badge bg-info text-dark">
                          <i class="fa fa-arrow-left me-1"></i>Backward
                        </span>
                      } @else if (node.direction === 'Forward') {
                        <span class="badge bg-success">
                          <i class="fa fa-arrow-right me-1"></i>Forward
                        </span>
                      } @else {
                        <span class="badge bg-light text-muted border">Origin</span>
                      }
                    </td>
                    <td>
                      <span class="badge bg-light text-dark border me-1">{{ node.referenceDocType }}</span>
                      <span class="fw-semibold">{{ node.referenceDocNo }}</span>
                    </td>
                    <td>
                      @if (node.workOrderNo) {
                        <span class="badge bg-primary-subtle text-primary border border-primary-subtle">
                          <i class="fa fa-industry me-1"></i>{{ node.workOrderNo }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td>{{ node.warehouseName || '-' }}</td>
                    <td class="text-end fw-semibold">
                      {{ node.quantity | number:'1.2-2' }}
                    </td>
                    <td class="text-nowrap small text-muted">
                      {{ node.postingDateTime | date:'mediumDate' }}
                    </td>
                    <td>
                      @if (node.supplierName) {
                        <div>
                          <i class="fa fa-truck me-1 text-info small"></i>
                          <span class="badge bg-info-subtle text-info border border-info-subtle me-1">Supplier</span>
                          {{ node.supplierName }}
                        </div>
                      }
                      @if (node.customerName) {
                        <div>
                          <i class="fa fa-building me-1 text-success small"></i>
                          <span class="badge bg-success-subtle text-success border border-success-subtle me-1">Customer</span>
                          {{ node.customerName }}
                        </div>
                      }
                      @if (!node.supplierName && !node.customerName) {
                        <span class="text-muted">-</span>
                      }
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class SerialNoAndBatchTraceabilityComponent implements OnInit {
  private traceabilityService = inject(SerialNoAndBatchTraceabilityService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  reportData = signal<SerialNoAndBatchTraceabilityReportDto | null>(null);
  items = signal<ItemDto[]>([]);
  isLoading = signal(false);

  selectedItemId = '';
  selectedSerialNo = '';
  selectedBatchNo = '';
  selectedDirection = 'Both';
  searchTerm = '';

  supplierCount = computed(() => {
    const nodes = this.reportData()?.nodes ?? [];
    return new Set(nodes.filter(n => n.supplierName).map(n => n.supplierName)).size;
  });

  customerCount = computed(() => {
    const nodes = this.reportData()?.nodes ?? [];
    return new Set(nodes.filter(n => n.customerName).map(n => n.customerName)).size;
  });

  filteredNodes = computed(() => {
    const data = this.reportData();
    if (!data?.nodes) return [];

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return data.nodes;

    return data.nodes.filter(n =>
      n.itemCode?.toLowerCase().includes(term) ||
      n.itemName?.toLowerCase().includes(term) ||
      n.serialNo?.toLowerCase().includes(term) ||
      n.batchNo?.toLowerCase().includes(term) ||
      n.referenceDocNo?.toLowerCase().includes(term) ||
      n.referenceDocType?.toLowerCase().includes(term) ||
      n.workOrderNo?.toLowerCase().includes(term) ||
      n.supplierName?.toLowerCase().includes(term) ||
      n.customerName?.toLowerCase().includes(term) ||
      n.warehouseName?.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.loadDropdowns();
    this.loadData();
  }

  private loadDropdowns(): void {
    this.itemService.getList({ maxResultCount: 1000 }).subscribe({
      next: res => this.items.set(res.items || []),
      error: () => {},
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId() || undefined;

    this.traceabilityService
      .getReport({
        companyId,
        itemId: this.selectedItemId || undefined,
        serialNo: this.selectedSerialNo.trim() || undefined,
        batchNo: this.selectedBatchNo.trim() || undefined,
        traceabilityDirection: this.selectedDirection,
        searchTerm: this.searchTerm.trim() || undefined,
      })
      .subscribe({
        next: res => {
          this.reportData.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.isLoading.set(false);
          this.toaster.error(err.message || 'Failed to load traceability genealogy');
        },
      });
  }

  exportCsv(): void {
    const nodes = this.filteredNodes();
    if (!nodes.length) return;

    const columns = [
      'IndentLevel',
      'Direction',
      'ItemCode',
      'ItemName',
      'SerialNo',
      'BatchNo',
      'VoucherType',
      'VoucherNo',
      'WorkOrderNo',
      'Warehouse',
      'Quantity',
      'PostingDateTime',
      'Supplier',
      'Customer',
      'BatchExpiry',
      'WarrantyExpiry',
    ];

    const exportRows = nodes.map(n => ({
      IndentLevel: n.indent,
      Direction: n.direction,
      ItemCode: n.itemCode,
      ItemName: n.itemName,
      SerialNo: n.serialNo || '',
      BatchNo: n.batchNo || '',
      VoucherType: n.referenceDocType,
      VoucherNo: n.referenceDocNo,
      WorkOrderNo: n.workOrderNo || '',
      Warehouse: n.warehouseName || '',
      Quantity: n.quantity,
      PostingDateTime: n.postingDateTime,
      Supplier: n.supplierName || '',
      Customer: n.customerName || '',
      BatchExpiry: n.batchExpiryDate || '',
      WarrantyExpiry: n.warrantyExpiryDate || '',
    }));

    exportToCsv('serial_and_batch_traceability.csv', exportRows, columns);
  }
}
