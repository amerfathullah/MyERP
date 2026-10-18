import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SerialNoWarrantyExpiryService } from '../../../proxy/inventory/serial-no-warranty-expiry.service';
import type {
  SerialNoWarrantyExpiryReportDto,
  SerialNoWarrantyExpiryRowDto,
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
  selector: 'app-serial-no-warranty-expiry',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-shield-alt me-2 text-primary"></i>{{ '::SerialNoWarrantyExpiry' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::SerialNoWarrantyExpiryDescription' | abpLocalization }}</small>
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
        <!-- Filter Row 1 -->
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
            <label class="form-label small fw-semibold">Expiry Type</label>
            <select class="form-select form-select-sm" [(ngModel)]="expiryType" (change)="loadData()">
              <option value="Both">Both (Warranty & AMC)</option>
              <option value="Warranty">Warranty Only</option>
              <option value="AMC">AMC Only</option>
            </select>
          </div>
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">Maintenance Status</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedMaintenanceStatus" (change)="loadData()">
              <option value="All">All Statuses</option>
              <option value="Under Warranty">{{ '::UnderWarranty' | abpLocalization }}</option>
              <option value="Under AMC">{{ '::UnderAMC' | abpLocalization }}</option>
              <option value="Out of Warranty">{{ '::OutOfWarranty' | abpLocalization }}</option>
              <option value="Out of AMC">{{ '::OutOfAMC' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter Row 2 -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-2 col-sm-6">
            <label class="form-label small fw-semibold">Expiring Within</label>
            <select class="form-select form-select-sm" [(ngModel)]="daysThreshold" (change)="loadData()">
              <option [ngValue]="null">Any Duration</option>
              <option [ngValue]="30">30 Days</option>
              <option [ngValue]="60">60 Days</option>
              <option [ngValue]="90">90 Days</option>
              <option [ngValue]="180">180 Days</option>
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
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'Search serial #, customer, item...'"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6 pt-md-3">
            <div class="form-check">
              <input
                type="checkbox"
                class="form-check-input"
                id="expiringSoonOnly"
                [(ngModel)]="expiringSoonOnly"
              />
              <label class="form-check-label small" for="expiringSoonOnly">
                {{ '::ExpiringSoon' | abpLocalization }} Only
              </label>
            </div>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (report()) {
          <div class="row g-3 mb-4">
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-light border-0 shadow-sm p-3 text-center">
                <span class="text-muted small">Total Serials</span>
                <span class="h5 mb-0 fw-bold">{{ report()!.totalCount }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-success-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-success small fw-semibold">{{ '::UnderWarranty' | abpLocalization }}</span>
                <span class="h5 mb-0 fw-bold text-success">{{ report()!.underWarrantyCount }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-info-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-info small fw-semibold">{{ '::UnderAMC' | abpLocalization }}</span>
                <span class="h5 mb-0 fw-bold text-info">{{ report()!.underAmcCount }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-secondary-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-secondary small fw-semibold">{{ '::OutOfWarranty' | abpLocalization }}</span>
                <span class="h5 mb-0 fw-bold text-secondary">{{ report()!.outOfWarrantyCount }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-danger-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-danger small fw-semibold">{{ '::OutOfAMC' | abpLocalization }}</span>
                <span class="h5 mb-0 fw-bold text-danger">{{ report()!.outOfAmcCount }}</span>
              </div>
            </div>
            <div class="col-md-2 col-sm-4 col-6">
              <div class="card bg-warning-subtle border-0 shadow-sm p-3 text-center">
                <span class="text-warning-emphasis small fw-semibold">{{ '::ExpiringSoon' | abpLocalization }}</span>
                <span class="h5 mb-0 fw-bold text-warning-emphasis">{{ report()!.expiringSoonCount }}</span>
              </div>
            </div>
          </div>
        }

        <!-- Report Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status"></div>
            <div class="small text-muted mt-2">{{ '::Loading' | abpLocalization }}</div>
          </div>
        } @else if (filteredRows().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-info-circle fa-2x mb-2 d-block"></i>
            {{ '::NoSerialNoWarrantyRecordsFound' | abpLocalization }}
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover table-bordered align-middle">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ '::SerialNumber' | abpLocalization }}</th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th>{{ '::Customer' | abpLocalization }}</th>
                  <th>{{ '::Warehouse' | abpLocalization }}</th>
                  <th>Maintenance Status</th>
                  <th>{{ '::WarrantyExpiryDate' | abpLocalization }}</th>
                  <th>{{ '::DaysToWarrantyExpiry' | abpLocalization }}</th>
                  <th>{{ '::AmcExpiryDate' | abpLocalization }}</th>
                  <th>{{ '::DaysToAmcExpiry' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.serialNoId; let idx = $index) {
                  <tr>
                    <td class="text-muted small">{{ idx + 1 }}</td>
                    <td>
                      <code class="fw-semibold text-primary">{{ row.serialNumber }}</code>
                    </td>
                    <td>
                      <div class="fw-semibold small">{{ row.itemCode }}</div>
                      <div class="text-muted small">{{ row.itemName }}</div>
                    </td>
                    <td class="small">{{ row.customerName || '-' }}</td>
                    <td class="small">{{ row.warehouseName || '-' }}</td>
                    <td>
                      <span [class]="getMaintenanceBadgeClass(row.maintenanceStatus)">
                        {{ row.maintenanceStatus || '-' }}
                      </span>
                    </td>
                    <td class="small">
                      @if (row.warrantyExpiryDate) {
                        <span [class.text-danger]="row.isWarrantyExpired">
                          {{ row.warrantyExpiryDate | date: 'mediumDate' }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="small">
                      @if (row.daysToWarrantyExpiry !== null && row.daysToWarrantyExpiry !== undefined) {
                        @if (row.isWarrantyExpired) {
                          <span class="badge bg-danger">Expired ({{ -row.daysToWarrantyExpiry }}d ago)</span>
                        } @else if (row.daysToWarrantyExpiry <= 30) {
                          <span class="badge bg-warning text-dark">{{ row.daysToWarrantyExpiry }}d left</span>
                        } @else {
                          <span class="text-muted">{{ row.daysToWarrantyExpiry }}d</span>
                        }
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="small">
                      @if (row.amcExpiryDate) {
                        <span [class.text-danger]="row.isAmcExpired">
                          {{ row.amcExpiryDate | date: 'mediumDate' }}
                        </span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td class="small">
                      @if (row.daysToAmcExpiry !== null && row.daysToAmcExpiry !== undefined) {
                        @if (row.isAmcExpired) {
                          <span class="badge bg-danger">Expired ({{ -row.daysToAmcExpiry }}d ago)</span>
                        } @else if (row.daysToAmcExpiry <= 30) {
                          <span class="badge bg-warning text-dark">{{ row.daysToAmcExpiry }}d left</span>
                        } @else {
                          <span class="text-muted">{{ row.daysToAmcExpiry }}d</span>
                        }
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
export class SerialNoWarrantyExpiryComponent implements OnInit {
  private readonly reportService = inject(SerialNoWarrantyExpiryService);
  private readonly itemService = inject(ItemService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly companyContext = inject(CompanyContextService);

  // Filters
  selectedWarehouseId = '';
  selectedItemId = '';
  expiryType = 'Both';
  selectedMaintenanceStatus = 'All';
  daysThreshold: number | null = null;
  fromDate = '';
  toDate = '';
  searchTerm = '';
  expiringSoonOnly = false;

  // State
  isLoading = signal<boolean>(false);
  report = signal<SerialNoWarrantyExpiryReportDto | null>(null);
  warehouses = signal<WarehouseDto[]>([]);
  serialItems = signal<ItemDto[]>([]);

  filteredRows = computed<SerialNoWarrantyExpiryRowDto[]>(() => {
    const data = this.report();
    if (!data?.rows) return [];

    let rows = data.rows;

    if (this.expiringSoonOnly) {
      rows = rows.filter(r => {
        const wSoon = r.daysToWarrantyExpiry !== null && r.daysToWarrantyExpiry !== undefined && r.daysToWarrantyExpiry >= 0 && r.daysToWarrantyExpiry <= 30;
        const aSoon = r.daysToAmcExpiry !== null && r.daysToAmcExpiry !== undefined && r.daysToAmcExpiry >= 0 && r.daysToAmcExpiry <= 30;
        return wSoon || aSoon;
      });
    }

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      rows = rows.filter(
        r =>
          r.serialNumber.toLowerCase().includes(term) ||
          r.itemCode.toLowerCase().includes(term) ||
          r.itemName.toLowerCase().includes(term) ||
          (r.customerName && r.customerName.toLowerCase().includes(term)) ||
          (r.warehouseName && r.warehouseName.toLowerCase().includes(term))
      );
    }

    return rows;
  });

  ngOnInit(): void {
    this.loadLookups();
    this.loadData();
  }

  loadLookups(): void {
    this.warehouseService.getList({ maxResultCount: 200 } as any).subscribe(res => {
      this.warehouses.set(res.items || []);
    });

    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(res => {
      const items = (res.items || []).filter(i => i.hasSerialNo);
      this.serialItems.set(items);
    });
  }

  loadData(): void {
    this.isLoading.set(true);

    this.reportService
      .getReport({
        companyId: this.companyContext.selectedCompanyId() || undefined,
        warehouseId: this.selectedWarehouseId || undefined,
        itemId: this.selectedItemId || undefined,
        expiryType: this.expiryType,
        maintenanceStatus: this.selectedMaintenanceStatus !== 'All' ? this.selectedMaintenanceStatus : undefined,
        daysThreshold: this.daysThreshold || undefined,
        fromDate: this.fromDate || undefined,
        toDate: this.toDate || undefined,
        searchTerm: this.searchTerm || undefined,
      })
      .subscribe({
        next: data => {
          this.report.set(data);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  getMaintenanceBadgeClass(status: string): string {
    switch (status) {
      case 'Under Warranty':
        return 'badge bg-success';
      case 'Under AMC':
        return 'badge bg-info text-dark';
      case 'Out of Warranty':
        return 'badge bg-secondary';
      case 'Out of AMC':
        return 'badge bg-danger';
      default:
        return 'badge bg-light text-dark';
    }
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const exportRows = rows.map(r => ({
      'Serial Number': r.serialNumber,
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Customer': r.customerName || '',
      'Warehouse': r.warehouseName || '',
      'Maintenance Status': r.maintenanceStatus,
      'Warranty Expiry Date': r.warrantyExpiryDate ? r.warrantyExpiryDate.substring(0, 10) : '',
      'Days to Warranty Expiry': r.daysToWarrantyExpiry ?? '',
      'Warranty Expired': r.isWarrantyExpired ? 'Yes' : 'No',
      'AMC Expiry Date': r.amcExpiryDate ? r.amcExpiryDate.substring(0, 10) : '',
      'Days to AMC Expiry': r.daysToAmcExpiry ?? '',
      'AMC Expired': r.isAmcExpired ? 'Yes' : 'No',
    }));

    const columns = [
      'Serial Number',
      'Item Code',
      'Item Name',
      'Customer',
      'Warehouse',
      'Maintenance Status',
      'Warranty Expiry Date',
      'Days to Warranty Expiry',
      'Warranty Expired',
      'AMC Expiry Date',
      'Days to AMC Expiry',
      'AMC Expired',
    ];

    exportToCsv('serial_no_warranty_expiry.csv', exportRows, columns);
  }
}
