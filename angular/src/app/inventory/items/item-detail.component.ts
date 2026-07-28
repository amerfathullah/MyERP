import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-item-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />

    @if (loading()) {
      <div class="d-flex justify-content-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (entity(); as item) {
      <!-- Header -->
      <div class="card mb-4">
        <div class="card-body d-flex align-items-center justify-content-between">
          <div>
            <h3 class="mb-1">
              <i class="fas fa-box me-2 text-primary"></i>{{ item.itemCode }} — {{ item.itemName }}
            </h3>
            <span class="badge bg-primary me-1">{{ item.itemType || 'Stock' }}</span>
            <span class="badge" [class]="item.isActive !== false ? 'bg-success' : 'bg-secondary'">
              {{ (item.isActive !== false ? '::Active' : '::Inactive') | abpLocalization }}
            </span>
            @if (item.hasSerialNo) {
              <span class="badge bg-info text-dark ms-1"><i class="fas fa-barcode me-1"></i>{{ '::SerialNo' | abpLocalization }}</span>
            }
            @if (item.hasBatchNo) {
              <span class="badge bg-info text-dark ms-1"><i class="fas fa-layer-group me-1"></i>{{ '::BatchNo' | abpLocalization }}</span>
            }
          </div>
          <a [routerLink]="['/inventory/items', entityId, 'edit']" class="btn btn-outline-primary">
            <i class="fas fa-pencil-alt me-1"></i> {{ '::Edit' | abpLocalization }}
          </a>
        </div>
      </div>

      <!-- Info Grid -->
      <div class="row mb-4">
        <!-- General Info -->
        <div class="col-md-4 mb-3">
          <div class="card h-100">
            <div class="card-header"><i class="fas fa-info-circle me-2"></i>{{ '::GeneralInfo' | abpLocalization }}</div>
            <div class="card-body">
              <table class="table table-sm table-borderless mb-0">
                <tbody>
                  <tr>
                    <th class="text-muted w-40">{{ '::ItemCode' | abpLocalization }}</th>
                    <td class="font-monospace">{{ item.itemCode }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::ItemName' | abpLocalization }}</th>
                    <td>{{ item.itemName }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::ItemGroup' | abpLocalization }}</th>
                    <td>{{ item.itemGroupName || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::UOM' | abpLocalization }}</th>
                    <td>{{ item.stockUom || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Brand' | abpLocalization }}</th>
                    <td>{{ item.brand || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Description' | abpLocalization }}</th>
                    <td>{{ item.description || '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Pricing Info -->
        <div class="col-md-4 mb-3">
          <div class="card h-100">
            <div class="card-header"><i class="fas fa-tags me-2"></i>{{ '::PricingInfo' | abpLocalization }}</div>
            <div class="card-body">
              <table class="table table-sm table-borderless mb-0">
                <tbody>
                  <tr>
                    <th class="text-muted w-40">{{ '::SellingPrice' | abpLocalization }}</th>
                    <td>{{ (item.standardSellingRate ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::BuyingPrice' | abpLocalization }}</th>
                    <td>{{ (item.standardBuyingRate ?? item.lastPurchaseRate ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::ValuationMethod' | abpLocalization }}</th>
                    <td>{{ item.valuationMethod || 'FIFO' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::ValuationRate' | abpLocalization }}</th>
                    <td>{{ (item.valuationRate ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Stock Info -->
        <div class="col-md-4 mb-3">
          <div class="card h-100">
            <div class="card-header"><i class="fas fa-warehouse me-2"></i>{{ '::StockInfo' | abpLocalization }}</div>
            <div class="card-body">
              <table class="table table-sm table-borderless mb-0">
                <tbody>
                  <tr>
                    <th class="text-muted w-40">{{ '::ReorderLevel' | abpLocalization }}</th>
                    <td>{{ item.reorderLevel ?? 0 }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::SafetyStock' | abpLocalization }}</th>
                    <td>{{ item.safetyStock ?? 0 }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::MinimumOrderQty' | abpLocalization }}</th>
                    <td>{{ item.minOrderQty ?? 0 }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::LeadTime' | abpLocalization }}</th>
                    <td>{{ item.leadTimeDays ?? 0 }} {{ '::Days' | abpLocalization }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <!-- Stock Balance -->
      <div class="card mb-4">
        <div class="card-header"><i class="fas fa-boxes me-2"></i>{{ '::StockBalance' | abpLocalization }}</div>
        <div class="card-body">
          @if (stockLoading()) {
            <div class="text-center py-3">
              <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
            </div>
          } @else if (stockBalance().length === 0) {
            <div class="text-center text-muted py-3">{{ '::NoStockBalanceData' | abpLocalization }}</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ActualQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReservedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AvailableQty' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of stockBalance(); track row.warehouseId) {
                    <tr>
                      <td>{{ row.warehouseName }}</td>
                      <td class="text-end">{{ row.actualQty | number:'1.0-2' }}</td>
                      <td class="text-end">{{ row.reservedQty | number:'1.0-2' }}</td>
                      <td class="text-end fw-bold" [class.text-danger]="row.availableQty < 0">
                        {{ row.availableQty | number:'1.0-2' }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- Quality Flags -->
      @if (item.inspectionRequired || item.qualityInspectionTemplate) {
        <div class="card mb-4">
          <div class="card-header"><i class="fas fa-clipboard-check me-2"></i>{{ '::QualityFlags' | abpLocalization }}</div>
          <div class="card-body">
            @if (item.inspectionRequired) {
              <span class="badge bg-warning text-dark me-2">
                <i class="fas fa-check-circle me-1"></i>{{ '::InspectionRequired' | abpLocalization }}
              </span>
            }
            @if (item.qualityInspectionTemplate) {
              <span class="text-muted">{{ '::Template' | abpLocalization }}: {{ item.qualityInspectionTemplate }}</span>
            }
          </div>
        </div>
      }

      <!-- Quick Actions -->
      <div class="card mb-4">
        <div class="card-body">
          <div class="d-flex flex-wrap gap-2">
            <a [routerLink]="['/inventory/stock-entries/new']" [queryParams]="{ itemCode: item.itemCode }" class="btn btn-outline-primary">
              <i class="fas fa-exchange-alt me-1"></i> {{ '::CreateStockEntry' | abpLocalization }}
            </a>
            <a [routerLink]="['/inventory/reports/stock-ledger']" [queryParams]="{ itemId: entityId }" class="btn btn-outline-secondary">
              <i class="fas fa-book me-1"></i> {{ '::ViewStockLedger' | abpLocalization }}
            </a>
          </div>
        </div>
      </div>

      <!-- Recent Stock Movements -->
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span><i class="fas fa-history me-2"></i>{{ '::RecentStockMovements' | abpLocalization }}</span>
          <a [routerLink]="['/inventory/reports/stock-ledger']" [queryParams]="{ itemId: entityId }" class="btn btn-link btn-sm p-0">
            View All →
          </a>
        </div>
        <div class="card-body p-0">
          @if (movementsLoading()) {
            <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
          } @else if (stockMovements().length === 0) {
            <div class="text-center text-muted py-3">{{ '::NoRecentMovements' | abpLocalization }}</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Date' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Balance' | abpLocalization }}</th>
                    <th>{{ '::Voucher' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (m of stockMovements(); track $index) {
                    <tr>
                      <td>{{ m.postingDate | date:'dd/MM/yy' }}</td>
                      <td>{{ m.warehouseName }}</td>
                      <td class="text-end" [class.text-success]="m.quantityChange > 0" [class.text-danger]="m.quantityChange < 0">
                        {{ m.quantityChange > 0 ? '+' : '' }}{{ m.quantityChange | number:'1.0-2' }}
                      </td>
                      <td class="text-end">{{ m.valuationRate | number:'1.2-4' }}</td>
                      <td class="text-end fw-bold">{{ m.balanceQty | number:'1.0-2' }}</td>
                      <td><span class="badge bg-secondary">{{ m.voucherType }}</span></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- Price History -->
      <div class="card mb-4">
        <div class="card-header"><i class="fas fa-chart-line me-2"></i>{{ '::PriceHistory' | abpLocalization }}</div>
        <div class="card-body p-0">
          @if (priceHistoryLoading()) {
            <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
          } @else if (priceHistory().length === 0) {
            <div class="text-center text-muted py-3">{{ '::NoPriceRecords' | abpLocalization }}</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::PriceList' | abpLocalization }}</th>
                    <th>{{ '::Type' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th>{{ '::Currency' | abpLocalization }}</th>
                    <th>{{ '::ValidFrom' | abpLocalization }}</th>
                    <th>{{ '::ValidUpto' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (p of priceHistory(); track p.id) {
                    <tr>
                      <td>{{ p.priceListName || '—' }}</td>
                      <td>
                        @if (p.isSelling) { <span class="badge bg-success-subtle text-success">Selling</span> }
                        @if (p.isBuying) { <span class="badge bg-primary-subtle text-primary">Buying</span> }
                      </td>
                      <td class="text-end fw-bold">{{ p.rate | number:'1.2-4' }}</td>
                      <td>{{ p.currency }}</td>
                      <td>{{ p.validFrom ? (p.validFrom | date:'dd/MM/yy') : '—' }}</td>
                      <td>{{ p.validUpto ? (p.validUpto | date:'dd/MM/yy') : '—' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- Where Used (BOM References) -->
      <div class="card mb-4">
        <div class="card-header"><i class="fas fa-sitemap me-2"></i>{{ '::WhereUsed' | abpLocalization }}</div>
        <div class="card-body p-0">
          @if (whereUsedLoading()) {
            <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
          } @else if (whereUsed().length === 0) {
            <div class="text-center text-muted py-3">Not used in any Bill of Materials</div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::BOM' | abpLocalization }}</th>
                    <th>{{ '::FGItem' | abpLocalization }}</th>
                    <th class="text-end">{{ '::QtyPerUnit' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (b of whereUsed(); track b.bomId) {
                    <tr>
                      <td><a [routerLink]="['/manufacturing/bom', b.bomId]" class="text-primary">{{ b.bomNumber }}</a></td>
                      <td>{{ b.fgItemCode }} — {{ b.fgItemName }}</td>
                      <td class="text-end">{{ b.quantityPerUnit | number:'1.0-4' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <!-- Item Variants (for template items) -->
      @if (item.hasVariants && variants().length > 0) {
        <div class="card mb-4">
          <div class="card-header"><i class="fas fa-clone me-2"></i>{{ '::Variants' | abpLocalization }} ({{ variants().length }})</div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-sm table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::ItemCode' | abpLocalization }}</th>
                    <th>{{ '::ItemName' | abpLocalization }}</th>
                    <th class="text-end">{{ '::SellingPrice' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (v of variants(); track v.id) {
                    <tr>
                      <td><a [routerLink]="['/inventory/items', v.id]" class="text-primary font-monospace">{{ v.itemCode }}</a></td>
                      <td>{{ v.itemName }}</td>
                      <td class="text-end">{{ (v.standardSellingPrice ?? 0) | number:'1.2-2' }}</td>
                      <td><span class="badge" [class.bg-success]="v.isActive" [class.bg-secondary]="!v.isActive">{{ v.isActive ? 'Active' : 'Inactive' }}</span></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Activity Log -->
      <app-activity-log [documentType]="'Item'" [documentId]="entityId" />
    }
  `,
  styles: [`
    .w-40 { width: 40%; }
  `],
})
export class ItemDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);

  entity = signal<any>(null);
  entityId = '';
  loading = signal(true);
  stockLoading = signal(true);
  stockBalance = signal<any[]>([]);
  movementsLoading = signal(true);
  stockMovements = signal<any[]>([]);
  priceHistoryLoading = signal(true);
  priceHistory = signal<any[]>([]);
  whereUsedLoading = signal(true);
  whereUsed = signal<any[]>([]);
  variants = signal<any[]>([]);

  ngOnInit() {
    this.entityId = this.route.snapshot.params['id'];
    this.loadEntity();
    this.loadStockBalance();
    this.loadRecentMovements();
    this.loadPriceHistory();
    this.loadWhereUsed();
  }

  private loadEntity() {
    this.http.get(`/api/app/item/${this.entityId}`).subscribe({
      next: (data: any) => {
        this.entity.set(data);
        this.loading.set(false);
        // Load variants if this is a template item
        if (data?.hasVariants) {
          this.loadVariants();
        }
      },
      error: () => this.loading.set(false),
    });
  }

  private loadStockBalance() {
    this.http.get<any>(`/api/app/bin/stock-balance`, {
      params: { itemId: this.entityId },
    }).subscribe({
      next: (data) => {
        this.stockBalance.set(data?.items ?? data ?? []);
        this.stockLoading.set(false);
      },
      error: () => this.stockLoading.set(false),
    });
  }

  private loadRecentMovements() {
    this.http.get<any[]>(`/api/app/item/${this.entityId}/recent-movements`, {
      params: { maxCount: '15' },
    }).subscribe({
      next: (data) => { this.stockMovements.set(data ?? []); this.movementsLoading.set(false); },
      error: () => this.movementsLoading.set(false),
    });
  }

  private loadPriceHistory() {
    this.http.get<any[]>(`/api/app/item/${this.entityId}/price-history`).subscribe({
      next: (data) => { this.priceHistory.set(data ?? []); this.priceHistoryLoading.set(false); },
      error: () => this.priceHistoryLoading.set(false),
    });
  }

  private loadWhereUsed() {
    this.http.get<any[]>(`/api/app/item/${this.entityId}/where-used`).subscribe({
      next: (data) => { this.whereUsed.set(data ?? []); this.whereUsedLoading.set(false); },
      error: () => this.whereUsedLoading.set(false),
    });
  }

  private loadVariants() {
    this.http.get<any[]>(`/api/app/item/${this.entityId}/variants`).subscribe({
      next: (data) => this.variants.set(data ?? []),
      error: () => {},
    });
  }
}
