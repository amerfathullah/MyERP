import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-item-price-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, RouterLink, PaginationComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-tags me-2"></i>{{ '::ItemPrices' | abpLocalization }}</h5>
          <div class="d-flex gap-2">
            <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()">
              <i class="fas fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
            <button class="btn btn-outline-warning btn-sm" (click)="showBulkUpdate.set(!showBulkUpdate())">
              <i class="fas fa-percent me-1"></i>{{ '::BulkUpdate' | abpLocalization }}
            </button>
            <button class="btn btn-primary btn-sm" (click)="showCreateForm.set(!showCreateForm())">
              <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Bulk Price Update -->
        @if (showBulkUpdate()) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-2 align-items-end">
              <div class="col-md-3">
                <label class="form-label">{{ '::PriceList' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="bulkPriceListId">
                  <option value="">{{ '::Select' | abpLocalization }}</option>
                  @for (pl of priceLists(); track pl.id) {
                    <option [value]="pl.id">{{ pl.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::PercentageChange' | abpLocalization }}</label>
                <div class="input-group input-group-sm">
                  <input type="number" class="form-control" [(ngModel)]="bulkPercentage" step="0.5" />
                  <span class="input-group-text">%</span>
                </div>
              </div>
              <div class="col-md-3">
                <button class="btn btn-warning btn-sm" [disabled]="!bulkPriceListId || bulkPercentage === 0" (click)="applyBulkUpdate()">
                  <i class="fas fa-sync me-1"></i>{{ '::Apply' | abpLocalization }}
                </button>
                <button class="btn btn-outline-secondary btn-sm ms-1" (click)="showBulkUpdate.set(false)">{{ '::Cancel' | abpLocalization }}</button>
              </div>
            </div>
          </div>
        }

        <!-- Create Form -->
        @if (showCreateForm()) {
          <div class="card-body border-bottom">
            <div class="row g-2">
              <div class="col-md-3">
                <select class="form-select form-select-sm" [(ngModel)]="newPrice.itemId">
                  <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                  @for (item of items(); track item.id) {
                    <option [value]="item.id">{{ item.itemCode }} — {{ item.itemName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <select class="form-select form-select-sm" [(ngModel)]="newPrice.priceListId">
                  <option value="">{{ '::PriceList' | abpLocalization }}</option>
                  @for (pl of priceLists(); track pl.id) {
                    <option [value]="pl.id">{{ pl.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <input type="number" class="form-control form-control-sm" [(ngModel)]="newPrice.rate" step="0.01" min="0" [placeholder]="'::Rate' | abpLocalization" />
              </div>
              <div class="col-md-2">
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newPrice.validFrom" />
              </div>
              <div class="col-md-2">
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newPrice.validUpto" />
              </div>
              <div class="col-md-1">
                <button class="btn btn-success btn-sm w-100" [disabled]="!newPrice.itemId || !newPrice.priceListId" (click)="createPrice()">
                  <i class="fas fa-check"></i>
                </button>
              </div>
            </div>
          </div>
        }

        <!-- Filters -->
        <div class="card-body border-bottom py-2">
          <div class="row g-2 align-items-center">
            <div class="col-md-3">
              <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm"
                     (keyup.enter)="loadData()" [placeholder]="'::Placeholder:Search' | abpLocalization" />
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="filterPriceListId" (change)="loadData()">
                <option value="">{{ '::AllPriceLists' | abpLocalization }}</option>
                @for (pl of priceLists(); track pl.id) {
                  <option [value]="pl.id">{{ pl.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-2">
              <small class="text-muted">{{ totalCount() }} {{ '::Results' | abpLocalization }}</small>
            </div>
          </div>
        </div>

        <!-- Table -->
        <div class="table-responsive">
          <table class="table table-sm table-hover mb-0">
            <thead class="table-light">
              <tr>
                <th>{{ '::Item' | abpLocalization }}</th>
                <th>{{ '::PriceList' | abpLocalization }}</th>
                <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                <th>{{ '::UOM' | abpLocalization }}</th>
                <th>{{ '::ValidFrom' | abpLocalization }}</th>
                <th>{{ '::ValidUpto' | abpLocalization }}</th>
                <th>{{ '::Party' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (price of prices(); track price.id) {
                <tr>
                  <td>
                    <a [routerLink]="['/inventory/items', price.itemId]" class="text-decoration-none">
                      <strong>{{ price.itemCode || '—' }}</strong>
                    </a>
                    <br><small class="text-muted">{{ price.itemName || '' }}</small>
                  </td>
                  <td><span class="badge bg-info text-dark">{{ price.priceListName || '—' }}</span></td>
                  <td class="text-end fw-bold font-monospace">{{ price.priceListRate | number:'1.2-4' }}</td>
                  <td>{{ price.uom }}</td>
                  <td>{{ price.validFrom ? (price.validFrom | date:'dd/MM/yyyy') : '—' }}</td>
                  <td>{{ price.validUpto ? (price.validUpto | date:'dd/MM/yyyy') : '—' }}</td>
                  <td>
                    @if (price.customerName) { <small class="text-success">{{ price.customerName }}</small> }
                    @else if (price.supplierName) { <small class="text-primary">{{ price.supplierName }}</small> }
                    @else { <small class="text-muted">{{ '::All' | abpLocalization }}</small> }
                  </td>
                  <td>
                    <button class="btn btn-link btn-sm text-danger p-0" (click)="deletePrice(price.id)">
                      <i class="fas fa-trash-alt"></i>
                    </button>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="8" class="text-center text-muted py-4">
                  <i class="fas fa-tags fa-2x mb-2 d-block opacity-50"></i>
                  {{ '::NoItemPricesYet' | abpLocalization }}
                </td></tr>
              }
            </tbody>
          </table>
        </div>

        <div class="card-footer">
          <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize" [currentPage]="currentPage"
                          (pageChange)="onPageChange($event)"></app-pagination>
        </div>
      </div>
    </div>
  `,
})
export class ItemPriceListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  prices = signal<any[]>([]);
  totalCount = signal(0);
  priceLists = signal<any[]>([]);
  items = signal<any[]>([]);
  currentPage = 0;
  pageSize = 20;
  searchTerm = '';
  filterPriceListId = '';

  showCreateForm = signal(false);
  showBulkUpdate = signal(false);

  newPrice = { itemId: '', priceListId: '', rate: 0, validFrom: '', validUpto: '' };
  bulkPriceListId = '';
  bulkPercentage = 0;

  ngOnInit(): void {
    this.loadPriceLists();
    this.loadItems();
    this.loadData();
  }

  loadData(): void {
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.filterPriceListId) params.priceListId = this.filterPriceListId;

    this.http.get<any>('/api/app/item-price', { params }).subscribe({
      next: (res) => {
        this.prices.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
      error: () => {}
    });
  }

  private loadPriceLists(): void {
    this.http.get<any>('/api/app/price-list', { params: { maxResultCount: 100, skipCount: 0 } }).subscribe({
      next: (res) => this.priceLists.set(res.items ?? []),
      error: () => {}
    });
  }

  private loadItems(): void {
    this.http.get<any>('/api/app/item', { params: { maxResultCount: 500, skipCount: 0 } }).subscribe({
      next: (res) => this.items.set(res.items ?? []),
      error: () => {}
    });
  }

  createPrice(): void {
    const dto = {
      itemId: this.newPrice.itemId,
      priceListId: this.newPrice.priceListId,
      priceListRate: this.newPrice.rate,
      uom: 'Unit',
      currencyCode: 'MYR',
      validFrom: this.newPrice.validFrom || undefined,
      validUpto: this.newPrice.validUpto || undefined,
    };
    this.http.post('/api/app/item-price', dto).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.showCreateForm.set(false);
        this.newPrice = { itemId: '', priceListId: '', rate: 0, validFrom: '', validUpto: '' };
        this.loadData();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  deletePrice(id: string): void {
    this.http.delete(`/api/app/item-price/${id}`).subscribe({
      next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadData(); },
      error: () => {}
    });
  }

  applyBulkUpdate(): void {
    const dto = { priceListId: this.bulkPriceListId, percentageChange: this.bulkPercentage };
    this.http.post<any>('/api/app/item-price/bulk-update', dto).subscribe({
      next: (res) => {
        this.toaster.success(`Updated ${res.updatedCount} prices by ${res.percentageApplied}%`);
        this.showBulkUpdate.set(false);
        this.bulkPercentage = 0;
        this.loadData();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  exportCsv(): void {
    exportToCsv('item-prices.csv', this.prices(), [
      'itemCode', 'itemName', 'priceListName', 'priceListRate', 'uom', 'currencyCode', 'validFrom', 'validUpto'
    ]);
  }
}
