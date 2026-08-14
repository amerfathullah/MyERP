import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ItemPriceService } from '../../proxy/inventory/item-price.service';
import { PriceListService } from '../../proxy/inventory/price-list.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { ItemPriceDto, PriceListDto, ItemDto } from '../../proxy/inventory/models';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  selector: 'app-item-price-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, PaginationComponent],
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

        <!-- Filter bar -->
        <div class="card-body border-bottom bg-light">
          <div class="row g-2 align-items-center">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm"
                     [(ngModel)]="searchTerm" (ngModelChange)="loadData()"
                     [placeholder]="'::SearchByItemOrCode' | abpLocalization" />
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="filterPriceListId" (ngModelChange)="loadData()">
                <option value="">{{ '::AllPriceLists' | abpLocalization }}</option>
                @for (pl of priceLists(); track pl.id) {
                  <option [value]="pl.id">{{ pl.name }}</option>
                }
              </select>
            </div>
          </div>
        </div>

        <!-- Bulk Update Form -->
        @if (showBulkUpdate()) {
          <div class="card-body bg-warning bg-opacity-10 border-bottom">
            <div class="row g-2 align-items-end">
              <div class="col-md-4">
                <label class="form-label small">{{ '::TargetPriceList' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="bulkPriceListId">
                  <option value="">{{ '::SelectPriceList' | abpLocalization }}</option>
                  @for (pl of priceLists(); track pl.id) {
                    <option [value]="pl.id">{{ pl.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label small">{{ '::PercentageChange' | abpLocalization }} (%)</label>
                <input type="number" class="form-control form-control-sm" [(ngModel)]="bulkPercentage" placeholder="e.g. 5 or -10" />
              </div>
              <div class="col-md-3">
                <button class="btn btn-warning btn-sm me-2" [disabled]="!bulkPriceListId || bulkPercentage === 0" (click)="applyBulkUpdate()">
                  {{ '::ApplyUpdate' | abpLocalization }}
                </button>
                <button class="btn btn-outline-secondary btn-sm" (click)="showBulkUpdate.set(false)">
                  {{ '::Cancel' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <!-- Create Form -->
        @if (showCreateForm()) {
          <div class="card-body bg-light border-bottom">
            <div class="row g-2">
              <div class="col-md-3">
                <label class="form-label small">{{ '::Item' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newPrice.itemId">
                  <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                  @for (item of items(); track item.id) {
                    <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label small">{{ '::PriceList' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newPrice.priceListId">
                  <option value="">{{ '::SelectPriceList' | abpLocalization }}</option>
                  @for (pl of priceLists(); track pl.id) {
                    <option [value]="pl.id">{{ pl.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label small">{{ '::Rate' | abpLocalization }}</label>
                <input type="number" class="form-control form-control-sm" [(ngModel)]="newPrice.rate" step="0.01" />
              </div>
              <div class="col-md-2">
                <label class="form-label small">{{ '::ValidFrom' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newPrice.validFrom" />
              </div>
              <div class="col-md-2">
                <label class="form-label small">{{ '::ValidUpto' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newPrice.validUpto" />
              </div>
            </div>
            <div class="mt-2">
              <button class="btn btn-success btn-sm me-2" [disabled]="!newPrice.itemId || !newPrice.priceListId || newPrice.rate <= 0" (click)="createPrice()">
                {{ '::Save' | abpLocalization }}
              </button>
              <button class="btn btn-outline-secondary btn-sm" (click)="showCreateForm.set(false)">
                {{ '::Cancel' | abpLocalization }}
              </button>
            </div>
          </div>
        }

        <!-- Table -->
        <div class="table-responsive">
          <table class="table table-hover align-middle mb-0">
            <thead class="table-light">
              <tr>
                <th>{{ '::ItemCode' | abpLocalization }}</th>
                <th>{{ '::ItemName' | abpLocalization }}</th>
                <th>{{ '::PriceList' | abpLocalization }}</th>
                <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                <th>{{ '::UOM' | abpLocalization }}</th>
                <th>{{ '::Currency' | abpLocalization }}</th>
                <th>{{ '::ValidFrom' | abpLocalization }}</th>
                <th>{{ '::ValidUpto' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (p of prices(); track p.id) {
                <tr>
                  <td><code>{{ p.itemCode }}</code></td>
                  <td>{{ p.itemName }}</td>
                  <td><span class="badge bg-secondary">{{ p.priceListName }}</span></td>
                  <td class="text-end font-monospace fw-bold">{{ p.priceListRate | number:'1.2-2' }}</td>
                  <td>{{ p.uom }}</td>
                  <td>{{ p.currencyCode }}</td>
                  <td>{{ p.validFrom | date:'shortDate' }}</td>
                  <td>{{ p.validUpto | date:'shortDate' }}</td>
                  <td class="text-end">
                    @if (p.id) {
                      <button class="btn btn-outline-danger btn-sm py-0 px-1" (click)="deletePrice(p.id)">
                        <i class="fas fa-trash-alt"></i>
                      </button>
                    }
                  </td>
                </tr>
              }
              @if (prices().length === 0) {
                <tr>
                  <td colspan="9" class="text-center py-4 text-muted">
                    {{ '::NoItemPricesFound' | abpLocalization }}
                  </td>
                </tr>
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
  private itemPriceService = inject(ItemPriceService);
  private priceListService = inject(PriceListService);
  private itemService = inject(ItemService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  prices = signal<ItemPriceDto[]>([]);
  totalCount = signal(0);
  priceLists = signal<PriceListDto[]>([]);
  items = signal<ItemDto[]>([]);
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
    this.itemPriceService.getList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      filter: this.searchTerm || undefined,
      priceListId: this.filterPriceListId || undefined,
    }).subscribe({
      next: (res) => {
        this.prices.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
      error: () => {}
    });
  }

  private loadPriceLists(): void {
    this.priceListService.getList({ maxResultCount: 100, skipCount: 0 }).subscribe({
      next: (res) => this.priceLists.set(res.items ?? []),
      error: () => {}
    });
  }

  private loadItems(): void {
    this.itemService.getList({ maxResultCount: 500, skipCount: 0 }).subscribe({
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
    this.itemPriceService.create(dto).subscribe({
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
    this.itemPriceService.delete(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadData(); },
      error: () => {}
    });
  }

  applyBulkUpdate(): void {
    const dto = { priceListId: this.bulkPriceListId, percentageChange: this.bulkPercentage };
    this.itemPriceService.bulkUpdate(dto).subscribe({
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
