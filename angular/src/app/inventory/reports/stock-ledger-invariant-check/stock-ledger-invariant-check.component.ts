import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { StockLedgerInvariantCheckService } from '../../../proxy/inventory/stock-ledger-invariant-check.service';
import { RepostItemValuationService } from '../../../proxy/inventory/repost-item-valuation.service';
import type {
  StockLedgerInvariantCheckReportDto,
  StockLedgerInvariantRowDto,
  StockLedgerVarianceRowDto,
  WarehouseDto,
  ItemDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-stock-ledger-invariant-check',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-stethoscope me-2 text-primary"></i>{{ '::StockLedgerInvariantCheck' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::StockLedgerInvariantCheckDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (activeTab() === 'item' && invariantReport() && invariantReport()!.rows.length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportInvariantCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
          @if (activeTab() === 'variance' && varianceRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportVarianceCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Navigation Tabs -->
        <ul class="nav nav-tabs mb-3">
          <li class="nav-item">
            <button
              class="nav-link"
              [class.active]="activeTab() === 'item'"
              (click)="setActiveTab('item')"
            >
              <i class="fa fa-search me-1"></i>{{ '::StockLedgerInvariantCheck' | abpLocalization }}
            </button>
          </li>
          <li class="nav-item">
            <button
              class="nav-link"
              [class.active]="activeTab() === 'variance'"
              (click)="setActiveTab('variance')"
            >
              <i class="fa fa-exclamation-triangle me-1"></i>{{ '::StockLedgerVariance' | abpLocalization }}
            </button>
          </li>
        </ul>

        @if (activeTab() === 'item') {
          <!-- Filters for Single Item Invariant Check -->
          <div class="row g-2 mb-3 align-items-end">
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">
                {{ '::Item' | abpLocalization }} <span class="text-danger">*</span>
              </label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedItemId">
                <option value="">-- {{ '::SelectItem' | abpLocalization }} --</option>
                @for (item of stockItems(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">
                {{ '::Warehouse' | abpLocalization }} <span class="text-danger">*</span>
              </label>
              <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId">
                <option value="">-- {{ '::SelectWarehouse' | abpLocalization }} --</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2 col-sm-6">
              <div class="form-check mt-4">
                <input
                  type="checkbox"
                  class="form-check-input"
                  id="showIncorrect"
                  [(ngModel)]="showIncorrectEntries"
                />
                <label class="form-check-label small" for="showIncorrect">
                  {{ '::ShowIncorrectEntries' | abpLocalization }}
                </label>
              </div>
            </div>
            <div class="col-md-2 col-sm-6">
              <button
                class="btn btn-primary btn-sm w-100"
                (click)="loadInvariantCheck()"
                [disabled]="isLoading() || !selectedItemId || !selectedWarehouseId"
              >
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>

          <!-- Alert message after reposting -->
          @if (actionMessage()) {
            <div class="alert alert-success alert-dismissible fade show small py-2 mb-3" role="alert">
              <i class="fa fa-check-circle me-1"></i>{{ actionMessage() }}
              <button type="button" class="btn-close btn-close-white small py-2" (click)="actionMessage.set('')"></button>
            </div>
          }

          <!-- Invariant Check Results -->
          @if (invariantReport()) {
            <!-- Summary Stats -->
            <div class="row g-3 mb-4">
              <div class="col-md-3 col-sm-6">
                <div class="border rounded p-3 text-center bg-light">
                  <div class="text-muted small">{{ '::Item' | abpLocalization }}</div>
                  <div class="fw-bold fs-6 text-truncate" [title]="invariantReport()!.itemCode + ' - ' + invariantReport()!.itemName">
                    {{ invariantReport()!.itemCode }}
                  </div>
                  <small class="badge bg-secondary">{{ invariantReport()!.valuationMethod }}</small>
                </div>
              </div>
              <div class="col-md-3 col-sm-6">
                <div class="border rounded p-3 text-center bg-light">
                  <div class="text-muted small">{{ '::Warehouse' | abpLocalization }}</div>
                  <div class="fw-bold fs-6 text-truncate" [title]="invariantReport()!.warehouseName">
                    {{ invariantReport()!.warehouseName }}
                  </div>
                  <small class="text-muted">{{ invariantReport()!.stockUom ?? 'Unit' }}</small>
                </div>
              </div>
              <div class="col-md-3 col-sm-6">
                <div class="border rounded p-3 text-center bg-light">
                  <div class="text-muted small">{{ '::TotalEntries' | abpLocalization }}</div>
                  <div class="fw-bold fs-5 text-primary">{{ invariantReport()!.totalEntries }}</div>
                  <small class="text-muted">{{ '::AuditEntries' | abpLocalization }}</small>
                </div>
              </div>
              <div class="col-md-3 col-sm-6">
                <div
                  class="border rounded p-3 text-center"
                  [ngClass]="invariantReport()!.violationCount > 0 ? 'bg-danger bg-opacity-10 border-danger' : 'bg-success bg-opacity-10 border-success'"
                >
                  <div class="text-muted small">{{ '::Discrepancies' | abpLocalization }}</div>
                  <div
                    class="fw-bold fs-5"
                    [class.text-danger]="invariantReport()!.violationCount > 0"
                    [class.text-success]="invariantReport()!.violationCount === 0"
                  >
                    {{ invariantReport()!.violationCount }}
                  </div>
                  <small [class.text-danger]="invariantReport()!.violationCount > 0" [class.text-success]="invariantReport()!.violationCount === 0">
                    @if (invariantReport()!.violationCount > 0) {
                      {{ '::ViolationAtStep' | abpLocalization }} #{{ (invariantReport()!.firstViolationIndex ?? 0) + 1 }}
                    } @else {
                      {{ '::AllInvariantsSatisfied' | abpLocalization }}
                    }
                  </small>
                </div>
              </div>
            </div>

            <!-- Invariant Rows Table -->
            <div class="table-responsive">
              <table class="table table-sm table-bordered table-hover align-middle mb-0 font-monospace small">
                <thead class="table-light">
                  <tr class="text-center align-middle">
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::Voucher' | abpLocalization }}</th>
                    <th>{{ '::QuantityChange' | abpLocalization }}</th>
                    <th>{{ '::QtyAfterTransaction' | abpLocalization }} (A)</th>
                    <th>{{ '::ExpectedBalanceQty' | abpLocalization }} (B)</th>
                    <th>{{ '::QtyDiscrepancy' | abpLocalization }} (A-B)</th>
                    <th>{{ '::BalanceValue' | abpLocalization }} (D)</th>
                    <th>{{ '::ExpectedBalanceValue' | abpLocalization }} (G)</th>
                    <th>{{ '::ValueDiscrepancy' | abpLocalization }} (G-D)</th>
                    <th>{{ '::FifoQueueQty' | abpLocalization }} (C)</th>
                    <th>{{ '::FifoQtyDiscrepancy' | abpLocalization }} (A-C)</th>
                    <th>{{ '::ValuationRate' | abpLocalization }}</th>
                    <th>{{ '::Action' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of invariantReport()!.rows; track row.sleId) {
                    <tr [class.table-danger]="row.isInvariantViolated">
                      <td>{{ row.postingDateTime | date: 'yyyy-MM-dd HH:mm' }}</td>
                      <td>
                        <span class="badge bg-light text-dark border">{{ row.voucherNo || row.voucherType }}</span>
                      </td>
                      <td
                        class="text-end"
                        [class.text-success]="row.quantityChange > 0"
                        [class.text-danger]="row.quantityChange < 0"
                      >
                        {{ row.quantityChange | number: '1.2-4' }}
                      </td>
                      <td class="text-end fw-semibold">{{ row.qtyAfterTransaction | number: '1.2-4' }}</td>
                      <td class="text-end">{{ row.expectedQtyAfterTransaction | number: '1.2-4' }}</td>
                      <td
                        class="text-end fw-bold"
                        [class.text-danger]="isNonZero(row.differenceInQty)"
                      >
                        {{ row.differenceInQty | number: '1.2-4' }}
                      </td>
                      <td class="text-end">{{ row.stockValue | number: '1.2-2' }}</td>
                      <td class="text-end">{{ row.stockValueFromDiff | number: '1.2-2' }}</td>
                      <td
                        class="text-end fw-bold"
                        [class.text-danger]="isNonZero(row.diffValueDiff)"
                      >
                        {{ row.diffValueDiff | number: '1.2-2' }}
                      </td>
                      <td class="text-end">
                        @if (row.fifoQueueQty !== null && row.fifoQueueQty !== undefined) {
                          {{ row.fifoQueueQty | number: '1.2-4' }}
                        } @else {
                          -
                        }
                      </td>
                      <td
                        class="text-end"
                        [class.text-danger]="isNonZero(row.fifoQtyDiff)"
                      >
                        @if (row.fifoQtyDiff !== null && row.fifoQtyDiff !== undefined) {
                          {{ row.fifoQtyDiff | number: '1.2-4' }}
                        } @else {
                          -
                        }
                      </td>
                      <td class="text-end">{{ row.valuationRate | number: '1.2-4' }}</td>
                      <td class="text-center">
                        @if (row.isInvariantViolated) {
                          <button
                            class="btn btn-xs btn-outline-danger"
                            (click)="repostValuation(row)"
                            [disabled]="isReposting()"
                            title="{{ '::CreateRepostingEntry' | abpLocalization }}"
                          >
                            <i class="fa fa-wrench me-1"></i>{{ '::Repost' | abpLocalization }}
                          </button>
                        } @else {
                          <i class="fa fa-check text-success" title="{{ '::Consistent' | abpLocalization }}"></i>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="13" class="text-center py-4 text-muted">
                        <i class="fa fa-info-circle me-1"></i>{{ '::NoStockLedgerInvariantEntriesFound' | abpLocalization }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        } @else {
          <!-- Filters for Cross-Warehouse Variance -->
          <div class="row g-2 mb-3 align-items-end">
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="varianceWarehouseId" (change)="loadVariance()">
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (w of warehouses(); track w.id) {
                  <option [value]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3 col-sm-6">
              <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="varianceItemId" (change)="loadVariance()">
                <option value="">{{ '::AllItems' | abpLocalization }}</option>
                @for (item of stockItems(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="form-check mt-4">
                <input
                  type="checkbox"
                  class="form-check-input"
                  id="showOnlyViolations"
                  [(ngModel)]="showOnlyViolations"
                  (change)="loadVariance()"
                />
                <label class="form-check-label small" for="showOnlyViolations">
                  {{ '::ShowOnlyViolations' | abpLocalization }}
                </label>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <button
                class="btn btn-primary btn-sm w-100"
                (click)="loadVariance()"
                [disabled]="isLoading()"
              >
                <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
              </button>
            </div>
          </div>

          <!-- Variance Table -->
          <div class="table-responsive">
            <table class="table table-sm table-bordered table-hover align-middle mb-0 font-monospace small">
              <thead class="table-light">
                <tr class="text-center align-middle">
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Voucher' | abpLocalization }}</th>
                  <th>{{ '::QuantityChange' | abpLocalization }}</th>
                  <th>{{ '::QtyAfterTransaction' | abpLocalization }}</th>
                  <th>{{ '::ExpectedBalanceQty' | abpLocalization }}</th>
                  <th>{{ '::QtyDiscrepancy' | abpLocalization }}</th>
                  <th>{{ '::BalanceValue' | abpLocalization }}</th>
                  <th>{{ '::ExpectedBalanceValue' | abpLocalization }}</th>
                  <th>{{ '::ValueDiscrepancy' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::Action' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of varianceRows(); track row.sleId) {
                  <tr [class.table-danger]="row.hasViolation">
                    <td class="fw-semibold">{{ row.itemCode }}</td>
                    <td>{{ row.warehouseName }}</td>
                    <td>{{ row.postingDateTime | date: 'yyyy-MM-dd HH:mm' }}</td>
                    <td><span class="badge bg-light text-dark border">{{ row.voucherNo || row.voucherType }}</span></td>
                    <td class="text-end">{{ row.quantityChange | number: '1.2-4' }}</td>
                    <td class="text-end">{{ row.qtyAfterTransaction | number: '1.2-4' }}</td>
                    <td class="text-end">{{ row.expectedQtyAfterTransaction | number: '1.2-4' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="isNonZero(row.differenceInQty)">
                      {{ row.differenceInQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end">{{ row.stockValue | number: '1.2-2' }}</td>
                    <td class="text-end">{{ row.stockValueFromDiff | number: '1.2-2' }}</td>
                    <td class="text-end fw-bold" [class.text-danger]="isNonZero(row.diffValueDiff)">
                      {{ row.diffValueDiff | number: '1.2-2' }}
                    </td>
                    <td class="text-center">
                      @if (row.hasViolation) {
                        <span class="badge bg-danger">{{ '::Discrepancy' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-success">{{ '::Consistent' | abpLocalization }}</span>
                      }
                    </td>
                    <td class="text-center">
                      <button
                        class="btn btn-xs btn-outline-primary"
                        (click)="inspectItemWarehouse(row.itemId, row.warehouseId)"
                        title="{{ '::Inspect' | abpLocalization }}"
                      >
                        <i class="fa fa-search me-1"></i>{{ '::Inspect' | abpLocalization }}
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="13" class="text-center py-4 text-muted">
                      <i class="fa fa-info-circle me-1"></i>{{ '::NoStockLedgerVarianceFound' | abpLocalization }}
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
export class StockLedgerInvariantCheckComponent implements OnInit {
  private invariantService = inject(StockLedgerInvariantCheckService);
  private repostService = inject(RepostItemValuationService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  activeTab = signal<'item' | 'variance'>('item');
  isLoading = signal(false);
  isReposting = signal(false);
  actionMessage = signal('');

  // Dropdowns
  warehouses = signal<WarehouseDto[]>([]);
  stockItems = signal<ItemDto[]>([]);

  // Tab 1 filters
  selectedItemId = '';
  selectedWarehouseId = '';
  showIncorrectEntries = false;
  invariantReport = signal<StockLedgerInvariantCheckReportDto | null>(null);

  // Tab 2 filters
  varianceWarehouseId = '';
  varianceItemId = '';
  showOnlyViolations = true;
  varianceRows = signal<StockLedgerVarianceRowDto[]>([]);

  ngOnInit(): void {
    this.loadDropdowns();
  }

  setActiveTab(tab: 'item' | 'variance'): void {
    this.activeTab.set(tab);
    if (tab === 'variance' && this.varianceRows().length === 0) {
      this.loadVariance();
    }
  }

  loadDropdowns(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.warehouseService.getList({ maxResultCount: 1000 }).subscribe({
      next: res => {
        let list = res.items || [];
        if (companyId) {
          list = list.filter(w => !w.companyId || w.companyId === companyId);
        }
        list = list.filter(w => !w.isGroup);
        this.warehouses.set(list);
      },
    });

    this.itemService.getList({ maxResultCount: 1000 }).subscribe({
      next: res => {
        const list = (res.items || []).filter(i => i.maintainStock);
        this.stockItems.set(list);
      },
    });
  }

  loadInvariantCheck(): void {
    if (!this.selectedItemId || !this.selectedWarehouseId) return;

    this.isLoading.set(true);
    this.actionMessage.set('');
    this.invariantService
      .getInvariantCheck({
        itemId: this.selectedItemId,
        warehouseId: this.selectedWarehouseId,
        showIncorrectEntries: this.showIncorrectEntries,
      })
      .subscribe({
        next: report => {
          this.invariantReport.set(report);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  loadVariance(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();
    this.invariantService
      .getStockLedgerVariance({
        companyId: companyId || undefined,
        warehouseId: this.varianceWarehouseId || undefined,
        itemId: this.varianceItemId || undefined,
        showOnlyViolations: this.showOnlyViolations,
      })
      .subscribe({
        next: res => {
          this.varianceRows.set(res.rows || []);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  repostValuation(row: StockLedgerInvariantRowDto): void {
    if (!this.invariantReport()) return;
    const rep = this.invariantReport()!;
    this.isReposting.set(true);

    this.repostService
      .create({
        companyId: rep.companyId,
        basedOn: 0,
        itemId: rep.itemId,
        warehouseId: rep.warehouseId,
        postingDate: row.postingDateTime,
        repostGlEntries: true,
      })
      .subscribe({
        next: () => {
          this.isReposting.set(false);
          this.actionMessage.set('Reposting Entry has been created successfully.');
          this.loadInvariantCheck();
        },
        error: () => this.isReposting.set(false),
      });
  }

  inspectItemWarehouse(itemId: string, warehouseId: string): void {
    this.selectedItemId = itemId;
    this.selectedWarehouseId = warehouseId;
    this.activeTab.set('item');
    this.loadInvariantCheck();
  }

  isNonZero(val: number | null | undefined): boolean {
    if (val === null || val === undefined) return false;
    return Math.abs(val) > 0.001;
  }

  exportInvariantCsv(): void {
    const report = this.invariantReport();
    if (!report || report.rows.length === 0) return;

    const data = report.rows.map(r => ({
      PostingDateTime: r.postingDateTime,
      VoucherType: r.voucherType,
      VoucherNo: r.voucherNo,
      QuantityChange: r.quantityChange,
      QtyAfterTransaction: r.qtyAfterTransaction,
      ExpectedQtyAfterTransaction: r.expectedQtyAfterTransaction,
      DifferenceInQty: r.differenceInQty,
      BalanceStockValue: r.stockValue,
      ExpectedStockValue: r.stockValueFromDiff,
      DiffValueDiff: r.diffValueDiff,
      FifoQueueQty: r.fifoQueueQty ?? '',
      FifoQtyDiff: r.fifoQtyDiff ?? '',
      ValuationRate: r.valuationRate,
      IsInvariantViolated: r.isInvariantViolated,
    }));
    const columns = [
      'PostingDateTime',
      'VoucherType',
      'VoucherNo',
      'QuantityChange',
      'QtyAfterTransaction',
      'ExpectedQtyAfterTransaction',
      'DifferenceInQty',
      'BalanceStockValue',
      'ExpectedStockValue',
      'DiffValueDiff',
      'FifoQueueQty',
      'FifoQtyDiff',
      'ValuationRate',
      'IsInvariantViolated',
    ];
    exportToCsv(`stock-ledger-invariant-${report.itemCode}-${report.warehouseName}.csv`, data, columns);
  }

  exportVarianceCsv(): void {
    const rows = this.varianceRows();
    if (rows.length === 0) return;

    const data = rows.map(r => ({
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      WarehouseName: r.warehouseName,
      PostingDateTime: r.postingDateTime,
      VoucherType: r.voucherType,
      VoucherNo: r.voucherNo,
      QuantityChange: r.quantityChange,
      QtyAfterTransaction: r.qtyAfterTransaction,
      ExpectedQtyAfterTransaction: r.expectedQtyAfterTransaction,
      DifferenceInQty: r.differenceInQty,
      StockValue: r.stockValue,
      ExpectedStockValue: r.stockValueFromDiff,
      DiffValueDiff: r.diffValueDiff,
      ValuationRate: r.valuationRate,
      HasViolation: r.hasViolation,
    }));
    const columns = [
      'ItemCode',
      'ItemName',
      'WarehouseName',
      'PostingDateTime',
      'VoucherType',
      'VoucherNo',
      'QuantityChange',
      'QtyAfterTransaction',
      'ExpectedQtyAfterTransaction',
      'DifferenceInQty',
      'StockValue',
      'ExpectedStockValue',
      'DiffValueDiff',
      'ValuationRate',
      'HasViolation',
    ];
    exportToCsv('stock-ledger-variance.csv', data, columns);
  }
}
