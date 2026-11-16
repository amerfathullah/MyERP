import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SerialAndBatchBundleService } from '../../proxy/inventory/serial-and-batch-bundle.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import type {
  ItemDto,
  SerialAndBatchBundleDto,
  WarehouseDto,
} from '../../proxy/inventory/models';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

export function filterBundlesBySearch(
  bundles: SerialAndBatchBundleDto[],
  searchTerm: string,
): SerialAndBatchBundleDto[] {
  const term = searchTerm.trim().toLowerCase();
  if (!term) {
    return bundles;
  }
  return bundles.filter(b =>
    [b.itemName, b.voucherType, b.bundleType, b.voucherId].some(v =>
      (v ?? '').toLowerCase().includes(term),
    ),
  );
}

@Component({
  selector: 'app-serial-and-batch-bundle-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fas fa-boxes text-primary me-2"></i>
            {{ '::SerialAndBatchBundles' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::SerialAndBatchBundlesDescription' | abpLocalization }}
          </p>
        </div>
        @if (filteredBundles().length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fas fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 border-0 shadow-sm">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Company' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="companyId"
                (ngModelChange)="onCompanyChange()"
              >
                @for (company of companies(); track company.id) {
                  <option [value]="company.id">{{ company.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::Item' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="itemId"
                (ngModelChange)="load()"
              >
                <option value="">{{ '::AllItems' | abpLocalization }}</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Warehouse' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="warehouseId"
                (ngModelChange)="load()"
              >
                <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
                @for (wh of warehouses(); track wh.id) {
                  <option [value]="wh.id">{{ wh.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::VoucherType' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="voucherType"
                (ngModelChange)="load()"
              >
                <option value="">{{ '::AllTypes' | abpLocalization }}</option>
                <option value="Stock Entry">Stock Entry</option>
                <option value="Delivery Note">Delivery Note</option>
                <option value="Purchase Receipt">Purchase Receipt</option>
                <option value="Sales Invoice">Sales Invoice</option>
                <option value="Purchase Invoice">Purchase Invoice</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'::ItemName' | abpLocalization"
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
              />
            </div>
            <div class="col-md-1">
              <button class="btn btn-sm btn-primary w-100" (click)="load()" [disabled]="loading()">
                <i class="fas fa-sync"></i>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Bundles Table -->
      <div class="card border-0 shadow-sm mb-3">
        <div class="table-responsive">
          <table class="table table-hover table-sm align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>{{ '::PostingDate' | abpLocalization }}</th>
                <th>{{ '::Item' | abpLocalization }}</th>
                <th>{{ '::BundleType' | abpLocalization }}</th>
                <th>{{ '::VoucherType' | abpLocalization }}</th>
                <th class="text-end">{{ '::TotalQty' | abpLocalization }}</th>
                <th class="text-end">{{ '::TotalAmount' | abpLocalization }}</th>
                <th class="text-end">{{ '::EntryCount' | abpLocalization }}</th>
                <th>{{ '::Status' | abpLocalization }}</th>
                <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
              </tr>
            </thead>
            <tbody>
              @if (loading()) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    <span class="spinner-border spinner-border-sm me-2"></span>
                    {{ '::Loading' | abpLocalization }}
                  </td>
                </tr>
              } @else if (filteredBundles().length === 0) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    {{ '::NoEntriesFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (bundle of filteredBundles(); track bundle.id) {
                  <tr [class.table-primary]="selectedBundle()?.id === bundle.id">
                    <td>{{ bundle.postingDate | date: 'yyyy-MM-dd' }}</td>
                    <td class="fw-semibold">{{ bundle.itemName || '-' }}</td>
                    <td>{{ bundle.bundleType }}</td>
                    <td>{{ bundle.voucherType || '-' }}</td>
                    <td class="text-end">{{ (bundle.totalQty ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (bundle.totalAmount ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ bundle.entryCount ?? 0 }}</td>
                    <td>
                      @if (bundle.isCancelled) {
                        <span class="badge bg-danger">{{ '::Cancelled' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                      }
                    </td>
                    <td class="text-end">
                      <button
                        class="btn btn-sm btn-outline-primary"
                        (click)="inspectBundle(bundle)"
                      >
                        <i class="fas fa-list me-1"></i>{{ '::Entries' | abpLocalization }}
                      </button>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Selected Bundle Entries -->
      @if (selectedBundle()) {
        <div class="card border-0 shadow-sm">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">
              {{ selectedBundle()!.itemName }} — {{ '::Entries' | abpLocalization }}
            </h6>
            <button class="btn btn-sm btn-outline-secondary" (click)="selectedBundle.set(null)">
              {{ '::Close' | abpLocalization }}
            </button>
          </div>
          <div class="table-responsive">
            <table class="table table-sm mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::SerialNo' | abpLocalization }}</th>
                  <th>{{ '::BatchNo' | abpLocalization }}</th>
                  <th class="text-end">{{ '::TotalQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                  <th>{{ '::FgSerialNo' | abpLocalization }}</th>
                  <th>{{ '::FgBatchNo' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of selectedBundle()!.entries ?? []; track $index) {
                  <tr>
                    <td class="font-monospace">{{ entry.serialNo || '-' }}</td>
                    <td class="font-monospace">{{ entry.batchNo || '-' }}</td>
                    <td class="text-end">{{ (entry.qty ?? 0) | number: '1.2-2' }}</td>
                    <td class="text-end">{{ (entry.rate ?? 0) | number: '1.2-2' }}</td>
                    <td class="font-monospace">{{ entry.fgSerialNo || '-' }}</td>
                    <td class="font-monospace">{{ entry.fgBatchNo || '-' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </div>
  `,
})
export class SerialAndBatchBundleListComponent implements OnInit {
  private bundleService = inject(SerialAndBatchBundleService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  companies = signal<CompanyDto[]>([]);
  items = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  bundles = signal<SerialAndBatchBundleDto[]>([]);
  selectedBundle = signal<SerialAndBatchBundleDto | null>(null);
  loading = signal(false);
  searchTerm = signal('');

  companyId = '';
  itemId = '';
  warehouseId = '';
  voucherType = '';

  filteredBundles = computed(() => filterBundlesBySearch(this.bundles(), this.searchTerm()));

  ngOnInit(): void {
    this.companyContext.load();
    this.warehouseService.getList({ maxResultCount: 200 }).subscribe({
      next: res => this.warehouses.set(res.items ?? []),
    });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const list = res.items ?? [];
        this.companies.set(list);
        this.companyId = this.companyContext.currentCompanyId() || list[0]?.id || '';
        this.loadItems();
        this.load();
      },
    });
  }

  onCompanyChange(): void {
    this.itemId = '';
    this.selectedBundle.set(null);
    this.loadItems();
    this.load();
  }

  loadItems(): void {
    this.itemService.getList({ companyId: this.companyId || undefined, maxResultCount: 200 }).subscribe({
      next: res => this.items.set(res.items ?? []),
    });
  }

  load(): void {
    this.loading.set(true);
    this.bundleService
      .getList({
        companyId: this.companyId || null,
        itemId: this.itemId || null,
        warehouseId: this.warehouseId || null,
        voucherType: this.voucherType || null,
        maxResultCount: 100,
        skipCount: 0,
      })
      .subscribe({
        next: res => {
          this.bundles.set(res.items ?? []);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  inspectBundle(bundle: SerialAndBatchBundleDto): void {
    if (!bundle.id) return;
    this.bundleService.get(bundle.id).subscribe({
      next: detail => this.selectedBundle.set(detail),
    });
  }

  exportCsv(): void {
    exportToCsv('serial-and-batch-bundles.csv', this.filteredBundles(), [
      'postingDate',
      'itemName',
      'bundleType',
      'voucherType',
      'totalQty',
      'totalAmount',
      'entryCount',
      'isCancelled',
    ]);
  }
}
