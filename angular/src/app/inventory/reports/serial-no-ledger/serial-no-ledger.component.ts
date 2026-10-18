import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SerialNoLedgerService } from '../../../proxy/inventory/serial-no-ledger.service';
import type {
  SerialNoLedgerReportDto,
  SerialNoLedgerRowDto,
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
  selector: 'app-serial-no-ledger',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-barcode me-2 text-primary"></i>{{ '::SerialNoLedger' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::SerialNoLedgerDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Item' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemId" (change)="loadData()">
              <option value="">{{ '::AllItems' | abpLocalization }}</option>
              @for (item of serialItems(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::SerialNo' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::SerialNo' | abpLocalization"
              [(ngModel)]="serialNumber"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="fromDate"
              (change)="loadData()"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input
              type="date"
              class="form-control form-control-sm"
              [(ngModel)]="toDate"
              (change)="loadData()"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter Row 2: Search -->
        <div class="row g-2 mb-4">
          <div class="col-md-6 col-sm-12">
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data()) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalEntries' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalEntries }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::InwardCount' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-success mt-1">
                    {{ data()!.inwardCount }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::OutwardCount' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.outwardCount }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ '::Loading' | abpLocalization }}</span>
            </div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-info-circle fa-3x mb-3 text-secondary opacity-50"></i>
            <p class="mb-0">{{ '::NoSerialNoLedgerFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover table-striped align-middle border">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::SerialNo' | abpLocalization }}</th>
                  <th>{{ '::ItemCode' | abpLocalization }}</th>
                  <th>{{ '::ItemName' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::VoucherType' | abpLocalization }}</th>
                  <th>{{ '::VoucherNo' | abpLocalization }}</th>
                  <th>{{ '::Party' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.serialNumber + '-' + row.postingDateTime + '-' + row.voucherNo; let idx = $index) {
                  <tr>
                    <td class="text-muted small">{{ idx + 1 }}</td>
                    <td class="small">{{ row.postingDateTime | date: 'yyyy-MM-dd HH:mm' }}</td>
                    <td>
                      <span class="badge bg-secondary-subtle text-secondary fw-semibold">
                        {{ row.serialNumber }}
                      </span>
                    </td>
                    <td class="fw-medium">{{ row.itemCode }}</td>
                    <td>{{ row.itemName }}</td>
                    <td>{{ row.warehouseName }}</td>
                    <td class="text-end">
                      <span [ngClass]="row.qty > 0 ? 'text-success fw-bold' : 'text-danger fw-bold'">
                        {{ row.qty > 0 ? '+' : '' }}{{ row.qty | number: '1.2-2' }}
                      </span>
                    </td>
                    <td class="text-end">{{ row.valuationRate | number: '1.2-2' }}</td>
                    <td>
                      <span class="badge" [ngClass]="getStatusBadgeClass(row.status)">
                        {{ row.status }}
                      </span>
                    </td>
                    <td>
                      <span class="badge bg-light text-dark border">{{ row.voucherType }}</span>
                    </td>
                    <td class="small text-muted font-monospace">{{ row.voucherNo || '-' }}</td>
                    <td class="small">
                      @if (row.partyName) {
                        <span>{{ row.partyType }}: {{ row.partyName }}</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
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
export class SerialNoLedgerComponent implements OnInit {
  private serialNoLedgerService = inject(SerialNoLedgerService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  serialItems = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  data = signal<SerialNoLedgerReportDto | null>(null);
  isLoading = signal(false);

  selectedWarehouseId = '';
  selectedItemId = '';
  serialNumber = '';
  fromDate = '';
  toDate = new Date().toISOString().slice(0, 10);
  searchTerm = '';

  filteredRows = computed<SerialNoLedgerRowDto[]>(() => {
    const report = this.data();
    if (!report || !report.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;

    return report.rows.filter(
      r =>
        r.serialNumber.toLowerCase().includes(term) ||
        r.itemCode.toLowerCase().includes(term) ||
        r.itemName.toLowerCase().includes(term) ||
        r.warehouseName.toLowerCase().includes(term) ||
        r.voucherNo.toLowerCase().includes(term) ||
        r.voucherType.toLowerCase().includes(term) ||
        (r.partyName && r.partyName.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadSerialItems();
    this.loadData();
  }

  private loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.warehouseService.getList({ maxResultCount: 200 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.warehouses.set(
          companyId ? items.filter(w => w.companyId === companyId) : items
        );
      },
      error: () => this.warehouses.set([]),
    });
  }

  private loadSerialItems(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.itemService.getList({ maxResultCount: 500 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.serialItems.set(
          items.filter(i => (!companyId || i.companyId === companyId) && i.hasSerialNo)
        );
      },
      error: () => this.serialItems.set([]),
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.selectedCompanyId();

    this.serialNoLedgerService
      .getReport({
        companyId: companyId || undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        serialNumber: this.serialNumber.trim() || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        searchTerm: this.searchTerm.trim() || undefined,
      })
      .subscribe({
        next: result => {
          this.data.set(result);
          this.isLoading.set(false);
        },
        error: () => {
          this.data.set(null);
          this.isLoading.set(false);
        },
      });
  }

  getStatusBadgeClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'active':
        return 'bg-success text-white';
      case 'delivered':
        return 'bg-primary text-white';
      case 'consumed':
        return 'bg-warning text-dark';
      case 'inactive':
        return 'bg-secondary text-white';
      default:
        return 'bg-light text-dark border';
    }
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (rows.length === 0) return;

    const exportRows = rows.map(r => ({
      PostingDateTime: r.postingDateTime,
      SerialNumber: r.serialNumber,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Warehouse: r.warehouseName,
      Qty: r.qty,
      ValuationRate: r.valuationRate,
      Status: r.status,
      VoucherType: r.voucherType,
      VoucherNo: r.voucherNo,
      PartyType: r.partyType || '',
      PartyName: r.partyName || '',
    }));

    const columns = [
      'PostingDateTime',
      'SerialNumber',
      'ItemCode',
      'ItemName',
      'Warehouse',
      'Qty',
      'ValuationRate',
      'Status',
      'VoucherType',
      'VoucherNo',
      'PartyType',
      'PartyName',
    ];

    const filename = `Serial_No_Ledger_${new Date().toISOString().slice(0, 10)}.csv`;
    exportToCsv(filename, exportRows, columns);
  }
}
