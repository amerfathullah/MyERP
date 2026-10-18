import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { AvailableSerialNoReportService } from '../../../proxy/inventory/available-serial-no-report.service';
import type {
  AvailableSerialNoReportDto,
  AvailableSerialNoRowDto,
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
  selector: 'app-available-serial-no',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-barcode me-2 text-primary"></i>{{ '::AvailableSerialNoReport' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::AvailableSerialNoReportDescription' | abpLocalization }}</small>
        </div>
        <div>
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-3 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of serialItems(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" (change)="loadData()" />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" (change)="loadData()" />
          </div>
          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-4 col-sm-6">
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-4 col-sm-6">
            <div class="form-check">
              <input
                type="checkbox"
                class="form-check-input"
                id="discrepanciesOnly"
                [(ngModel)]="discrepanciesOnly"
              />
              <label class="form-check-label small" for="discrepanciesOnly">
                {{ '::DiscrepanciesOnly' | abpLocalization }}
              </label>
            </div>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (report()) {
          <div class="row g-3 mb-4">
            <div class="col-md-2 col-sm-4 col-6">
              <div class="border rounded p-3 text-center bg-light">
                <div class="text-muted small">{{ '::TotalTransactions' | abpLocalization }}</div>
                <div class="fw-bold fs-6 text-primary">{{ report()!.totalTransactions }}</div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="border rounded p-3 text-center bg-light">
                <div class="text-muted small">{{ '::TotalInQty' | abpLocalization }}</div>
                <div class="fw-bold fs-6 text-success">{{ report()!.totalInQty | number: '1.2-4' }}</div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="border rounded p-3 text-center bg-light">
                <div class="text-muted small">{{ '::TotalOutQty' | abpLocalization }}</div>
                <div class="fw-bold fs-6 text-danger">{{ report()!.totalOutQty | number: '1.2-4' }}</div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="border rounded p-3 text-center bg-light">
                <div class="text-muted small">{{ '::BalanceQuantity' | abpLocalization }}</div>
                <div class="fw-bold fs-6 text-dark">{{ report()!.finalBalanceQty | number: '1.2-4' }}</div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="border rounded p-3 text-center bg-light">
                <div class="text-muted small">{{ '::BalanceSerialCount' | abpLocalization }}</div>
                <div class="fw-bold fs-6 text-info">{{ report()!.finalAvailableSerialCount }}</div>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div
                class="border rounded p-3 text-center"
                [ngClass]="report()!.discrepancyCount > 0 ? 'bg-danger bg-opacity-10 border-danger' : 'bg-success bg-opacity-10 border-success'"
              >
                <div class="text-muted small">{{ '::Discrepancies' | abpLocalization }}</div>
                <div
                  class="fw-bold fs-6"
                  [class.text-danger]="report()!.discrepancyCount > 0"
                  [class.text-success]="report()!.discrepancyCount === 0"
                >
                  {{ report()!.discrepancyCount }}
                </div>
              </div>
            </div>
          </div>

          <!-- Transactions Table -->
          <div class="table-responsive">
            <table class="table table-sm table-bordered table-hover align-middle mb-0 font-monospace small">
              <thead class="table-light">
                <tr class="text-center align-middle">
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>{{ '::InQty' | abpLocalization }}</th>
                  <th>{{ '::OutQty' | abpLocalization }}</th>
                  <th>{{ '::BalanceQuantity' | abpLocalization }}</th>
                  <th>{{ '::SerialNosMoved' | abpLocalization }}</th>
                  <th>{{ '::BalanceSerialNo' | abpLocalization }}</th>
                  <th>{{ '::BalanceSerialCount' | abpLocalization }}</th>
                  <th>{{ '::ValuationRate' | abpLocalization }}</th>
                  <th>{{ '::Voucher' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.sleId) {
                  <tr [class.table-danger]="row.hasDiscrepancy">
                    <td>{{ row.postingDateTime | date: 'yyyy-MM-dd HH:mm' }}</td>
                    <td>
                      <div class="fw-semibold">{{ row.itemCode }}</div>
                      <small class="text-muted text-truncate d-block" style="max-width: 140px;">{{ row.itemName }}</small>
                    </td>
                    <td>{{ row.warehouseName }}</td>
                    <td class="text-end" [class.text-success]="row.inQty > 0">
                      {{ row.inQty > 0 ? (row.inQty | number: '1.2-4') : '-' }}
                    </td>
                    <td class="text-end" [class.text-danger]="row.outQty > 0">
                      {{ row.outQty > 0 ? (row.outQty | number: '1.2-4') : '-' }}
                    </td>
                    <td class="text-end fw-bold">{{ row.balanceQty | number: '1.2-4' }}</td>
                    <td>
                      <div class="d-flex flex-wrap gap-1" style="max-width: 180px;">
                        @for (sn of row.serialNosMoved; track sn) {
                          <span
                            class="badge"
                            [ngClass]="row.inQty > 0 ? 'bg-success bg-opacity-75' : 'bg-danger bg-opacity-75'"
                          >
                            {{ sn }}
                          </span>
                        } @empty {
                          <span class="text-muted">-</span>
                        }
                      </div>
                    </td>
                    <td>
                      <div class="d-flex flex-wrap gap-1" style="max-width: 250px;">
                        @for (sn of row.balanceSerialNos; track sn) {
                          <span class="badge bg-secondary">{{ sn }}</span>
                        } @empty {
                          <span class="text-muted">-</span>
                        }
                      </div>
                    </td>
                    <td class="text-end fw-bold" [class.text-danger]="row.hasDiscrepancy">
                      {{ row.balanceSerialCount }}
                    </td>
                    <td class="text-end">{{ row.valuationRate | number: '1.2-4' }}</td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.voucherNo || row.voucherType }}</span>
                    </td>
                    <td class="text-center">
                      @if (row.hasDiscrepancy) {
                        <span class="badge bg-danger">{{ '::Discrepancy' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-success">{{ '::Consistent' | abpLocalization }}</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="12" class="text-center py-4 text-muted">
                      <i class="fa fa-info-circle me-1"></i>{{ '::NoAvailableSerialNoFound' | abpLocalization }}
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
export class AvailableSerialNoComponent implements OnInit {
  private service = inject(AvailableSerialNoReportService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  isLoading = signal(false);
  report = signal<AvailableSerialNoReportDto | null>(null);

  warehouses = signal<WarehouseDto[]>([]);
  serialItems = signal<ItemDto[]>([]);

  selectedWarehouseId = '';
  selectedItemId = '';
  fromDate = '';
  toDate = '';
  searchTerm = '';
  discrepanciesOnly = false;

  filteredRows = computed(() => {
    const rep = this.report();
    if (!rep) return [];

    let list = rep.rows;
    if (this.discrepanciesOnly) {
      list = list.filter(r => r.hasDiscrepancy);
    }
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.trim().toLowerCase();
      list = list.filter(
        r =>
          r.itemCode.toLowerCase().includes(term) ||
          r.itemName.toLowerCase().includes(term) ||
          r.warehouseName.toLowerCase().includes(term) ||
          (r.voucherNo && r.voucherNo.toLowerCase().includes(term)) ||
          r.serialNosMoved.some(s => s.toLowerCase().includes(term)) ||
          r.balanceSerialNos.some(s => s.toLowerCase().includes(term))
      );
    }
    return list;
  });

  ngOnInit(): void {
    this.loadDropdowns();
    this.loadData();
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
        const list = (res.items || []).filter(i => i.hasSerialNo);
        this.serialItems.set(list);
      },
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();
    this.service
      .getReport({
        companyId: companyId || undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        searchTerm: this.searchTerm || undefined,
      })
      .subscribe({
        next: res => {
          this.report.set(res);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const data = rows.map(r => ({
      PostingDateTime: r.postingDateTime,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      WarehouseName: r.warehouseName,
      InQty: r.inQty,
      OutQty: r.outQty,
      BalanceQty: r.balanceQty,
      SerialNosMoved: r.serialNosMoved.join('; '),
      BalanceSerialNos: r.balanceSerialNos.join('; '),
      BalanceSerialCount: r.balanceSerialCount,
      ValuationRate: r.valuationRate,
      VoucherType: r.voucherType,
      VoucherNo: r.voucherNo,
      HasDiscrepancy: r.hasDiscrepancy,
    }));

    const columns = [
      'PostingDateTime',
      'ItemCode',
      'ItemName',
      'WarehouseName',
      'InQty',
      'OutQty',
      'BalanceQty',
      'SerialNosMoved',
      'BalanceSerialNos',
      'BalanceSerialCount',
      'ValuationRate',
      'VoucherType',
      'VoucherNo',
      'HasDiscrepancy',
    ];

    exportToCsv('available-serial-no.csv', data, columns);
  }
}
