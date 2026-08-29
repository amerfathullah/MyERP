import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ItemService } from '../../proxy/inventory/item.service';
import type { ItemDto } from '../../proxy/inventory/models';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { CreateVariantDialogComponent } from './create-variant-dialog.component';

@Component({
  selector: 'app-item-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, CreateVariantDialogComponent],
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
                    <td>{{ item.itemGroup || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::UOM' | abpLocalization }}</th>
                    <td>{{ item.uom || '—' }}</td>
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
                    <td>{{ (item.standardSellingPrice ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::BuyingPrice' | abpLocalization }}</th>
                    <td>{{ (item.standardBuyingPrice ?? 0) | number:'1.2-2' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::ValuationMethod' | abpLocalization }}</th>
                    <td>{{ item.valuationMethod || 'FIFO' }}</td>
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
      @if (item.inspectionRequiredBeforePurchase || item.inspectionRequiredBeforeDelivery) {
        <div class="card mb-4">
          <div class="card-header"><i class="fas fa-clipboard-check me-2"></i>{{ '::QualityFlags' | abpLocalization }}</div>
          <div class="card-body">
            @if (item.inspectionRequiredBeforePurchase) {
              <span class="badge bg-warning text-dark me-2">
                <i class="fas fa-check-circle me-1"></i>{{ '::InspectionRequiredBeforePurchase' | abpLocalization }}
              </span>
            }
            @if (item.inspectionRequiredBeforeDelivery) {
              <span class="badge bg-warning text-dark me-2">
                <i class="fas fa-check-circle me-1"></i>{{ '::InspectionRequiredBeforeDelivery' | abpLocalization }}
              </span>
            }
          </div>
        </div>
      }

      <!-- Quick Actions -->
      <div class="card mb-4">
        <div class="card-body">
          <div class="d-flex flex-wrap gap-2">
            <button class="btn btn-outline-success" (click)="showTransferForm.set(true)" [disabled]="showTransferForm()">
              <i class="fas fa-right-left me-1"></i> {{ '::QuickTransfer' | abpLocalization }}
            </button>
            <a [routerLink]="['/inventory/stock-entries/new']" [queryParams]="{ itemCode: item.itemCode }" class="btn btn-outline-primary">
              <i class="fas fa-exchange-alt me-1"></i> {{ '::CreateStockEntry' | abpLocalization }}
            </a>
            <a [routerLink]="['/inventory/reports/stock-ledger']" [queryParams]="{ itemId: entityId }" class="btn btn-outline-secondary">
              <i class="fas fa-book me-1"></i> {{ '::ViewStockLedger' | abpLocalization }}
            </a>
            @if (isLowStock()) {
              <a [routerLink]="['/purchasing/orders/new']" [queryParams]="{ itemId: entityId }" class="btn btn-outline-warning">
                <i class="fas fa-cart-plus me-1"></i> {{ '::CreatePurchaseOrder' | abpLocalization }}
              </a>
            }
          </div>
        </div>
      </div>

      <!-- Quick Stock Transfer Form -->
      @if (showTransferForm()) {
        <div class="card mb-4 border-success">
          <div class="card-header bg-success-subtle d-flex justify-content-between align-items-center">
            <span><i class="fas fa-right-left me-2"></i>{{ '::QuickStockTransfer' | abpLocalization }}</span>
            <button type="button" class="btn-close" (click)="cancelTransfer()"></button>
          </div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-3">
                <label class="form-label">{{ '::SourceWarehouse' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="transferSourceWarehouse">
                  <option value="">{{ '::SelectWarehouse' | abpLocalization }}</option>
                  @for (w of transferWarehouses(); track w.id) {
                    <option [value]="w.id">{{ w.name }} ({{ getWarehouseQty(w.id) }})</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::TargetWarehouse' | abpLocalization }}</label>
                <select class="form-select" [(ngModel)]="transferTargetWarehouse">
                  <option value="">{{ '::SelectWarehouse' | abpLocalization }}</option>
                  @for (w of transferWarehouses(); track w.id) {
                    <option [value]="w.id" [disabled]="w.id === transferSourceWarehouse">{{ w.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::Qty' | abpLocalization }}</label>
                <input type="number" class="form-control" [(ngModel)]="transferQty" min="0.01" step="1" />
              </div>
              <div class="col-md-4 d-flex align-items-end gap-2">
                <button class="btn btn-success" (click)="submitTransfer()" [disabled]="isTransferring() || !canTransfer()">
                  @if (isTransferring()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fas fa-check me-1"></i>{{ '::Transfer' | abpLocalization }}
                </button>
                <button class="btn btn-outline-secondary" (click)="cancelTransfer()">{{ '::Cancel' | abpLocalization }}</button>
              </div>
            </div>
            @if (transferSourceWarehouse) {
              <div class="mt-2">
                <small class="text-muted">{{ '::AvailableInSource' | abpLocalization }}: <strong>{{ getWarehouseQty(transferSourceWarehouse) }}</strong></small>
              </div>
            }
          </div>
        </div>
      }

      <!-- Recent Stock Movements -->
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span><i class="fas fa-history me-2"></i>{{ '::RecentStockMovements' | abpLocalization }}</span>
          <a [routerLink]="['/inventory/reports/stock-ledger']" [queryParams]="{ itemId: entityId }" class="btn btn-link btn-sm p-0">
            {{ '::ViewAll' | abpLocalization }} →
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
            <div class="text-center text-muted py-3">{{ '::NotUsedInAnyBOM' | abpLocalization }}</div>
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

      <!-- Transaction Summary (Purchase/Sales Activity) -->
      <div class="card mb-4">
        <div class="card-header"><i class="fas fa-chart-bar me-2"></i>{{ '::TransactionSummary' | abpLocalization }}</div>
        <div class="card-body">
          @if (txSummaryLoading()) {
            <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
          } @else if (txSummary()) {
            <div class="row mb-3">
              <div class="col-md-6">
                <h6 class="text-primary"><i class="fas fa-shopping-cart me-1"></i>{{ '::PurchaseActivity' | abpLocalization }} <small class="text-muted">({{ '::Last12Months' | abpLocalization }})</small></h6>
                <div class="row g-2">
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::Orders' | abpLocalization }}</div>
                      <div class="fw-bold fs-5">{{ txSummary()!.purchaseOrderCount }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::TotalQty' | abpLocalization }}</div>
                      <div class="fw-bold fs-5">{{ txSummary()!.totalPurchasedQty | number:'1.0-2' }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::TotalValue' | abpLocalization }}</div>
                      <div class="fw-bold text-primary">{{ txSummary()!.totalPurchasedValue | number:'1.2-2' }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::LastRate' | abpLocalization }}</div>
                      <div class="fw-bold">{{ txSummary()!.lastPurchaseRate ? (txSummary()!.lastPurchaseRate | number:'1.2-4') : '—' }}</div>
                    </div>
                  </div>
                </div>
              </div>
              <div class="col-md-6">
                <h6 class="text-success"><i class="fas fa-tags me-1"></i>{{ '::SalesActivity' | abpLocalization }} <small class="text-muted">({{ '::Last12Months' | abpLocalization }})</small></h6>
                <div class="row g-2">
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::Orders' | abpLocalization }}</div>
                      <div class="fw-bold fs-5">{{ txSummary()!.salesOrderCount }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::TotalQty' | abpLocalization }}</div>
                      <div class="fw-bold fs-5">{{ txSummary()!.totalSoldQty | number:'1.0-2' }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::TotalValue' | abpLocalization }}</div>
                      <div class="fw-bold text-success">{{ txSummary()!.totalSoldValue | number:'1.2-2' }}</div>
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="border rounded p-2 text-center">
                      <div class="text-muted small">{{ '::AvgRate' | abpLocalization }}</div>
                      <div class="fw-bold">{{ txSummary()!.averageSellingRate ? (txSummary()!.averageSellingRate | number:'1.2-4') : '—' }}</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
            @if (txSummary()!.daysOfStockRemaining > 0) {
              <div class="d-flex align-items-center gap-2">
                <i class="fas fa-clock text-info"></i>
                <span>{{ '::DaysOfStockRemaining' | abpLocalization }}: <strong>{{ txSummary()!.daysOfStockRemaining }}</strong></span>
                @if (txSummary()!.isLowStock) {
                  <span class="badge bg-danger">{{ '::LowStock' | abpLocalization }}</span>
                }
              </div>
            }
          } @else {
            <div class="text-center text-muted py-3">{{ '::NoTransactionData' | abpLocalization }}</div>
          }
        </div>
      </div>

      <!-- Item Variants (for template items) -->
      @if (item.hasVariants) {
        <div class="card mb-4">
          <div class="card-header d-flex justify-content-between align-items-center">
            <span><i class="fas fa-clone me-2"></i>{{ '::Variants' | abpLocalization }} ({{ variants().length }})</span>
            <button class="btn btn-sm btn-outline-primary" (click)="openCreateVariant()">
              <i class="fas fa-plus me-1"></i>{{ '::CreateVariant' | abpLocalization }}
            </button>
          </div>
          @if (variants().length > 0) {
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
          } @else {
            <div class="card-body text-center text-muted py-3">{{ '::NoVariantsYet' | abpLocalization }}</div>
          }
        </div>
      }

      <app-create-variant-dialog
        [visible]="showCreateVariant()"
        [templateItemId]="entityId"
        [templateName]="item.itemName"
        (closed)="showCreateVariant.set(false)"
        (created)="onVariantCreated()"
      />

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
  private itemService = inject(ItemService);
  private stockBalanceService = inject(StockBalanceService);
  private stockEntryService = inject(StockEntryService);
  private warehouseService = inject(WarehouseService);

  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  entity = signal<ItemDto | null>(null);
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
  showCreateVariant = signal(false);
  txSummary = signal<any>(null);
  txSummaryLoading = signal(true);

  // Quick Transfer
  showTransferForm = signal(false);
  transferWarehouses = signal<any[]>([]);
  transferSourceWarehouse = '';
  transferTargetWarehouse = '';
  transferQty = 1;
  isTransferring = signal(false);

  ngOnInit() {
    this.entityId = this.route.snapshot.params['id'];
    this.loadEntity();
    this.loadStockBalance();
    this.loadRecentMovements();
    this.loadPriceHistory();
    this.loadWhereUsed();
    this.loadWarehouses();
    this.loadTransactionSummary();
  }

  private loadEntity() {
    this.itemService.get(this.entityId).subscribe({
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
    this.stockBalanceService.getItemStock(this.entityId).subscribe({
      next: (data: any) => {
        this.stockBalance.set(data ?? []);
        this.stockLoading.set(false);
      },
      error: () => this.stockLoading.set(false),
    });
  }

  private loadRecentMovements() {
    this.itemService.getRecentMovements(this.entityId, 15).subscribe({
      next: (data: any) => { this.stockMovements.set(data ?? []); this.movementsLoading.set(false); },
      error: () => this.movementsLoading.set(false),
    });
  }

  private loadPriceHistory() {
    this.itemService.getPriceHistory(this.entityId).subscribe({
      next: (data: any) => { this.priceHistory.set(data ?? []); this.priceHistoryLoading.set(false); },
      error: () => this.priceHistoryLoading.set(false),
    });
  }

  private loadWhereUsed() {
    this.itemService.getWhereUsed(this.entityId).subscribe({
      next: (data: any) => { this.whereUsed.set(data ?? []); this.whereUsedLoading.set(false); },
      error: () => this.whereUsedLoading.set(false),
    });
  }

  private loadVariants() {
    this.itemService.getVariants(this.entityId).subscribe({
      next: (data: any) => this.variants.set(data ?? []),
      error: () => {},
    });
  }

  openCreateVariant(): void {
    this.showCreateVariant.set(true);
  }

  onVariantCreated(): void {
    this.showCreateVariant.set(false);
    this.loadVariants();
  }

  private loadTransactionSummary() {
    this.itemService.getTransactionSummary(this.entityId).subscribe({
      next: (data: any) => { this.txSummary.set(data); this.txSummaryLoading.set(false); },
      error: () => this.txSummaryLoading.set(false),
    });
  }

  private loadWarehouses() {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (data: any) => this.transferWarehouses.set((data?.items ?? []).filter((w: any) => !w.isGroup)),
      error: () => {},
    });
  }

  getWarehouseQty(warehouseId: string): number {
    const row = this.stockBalance().find(b => b.warehouseId === warehouseId);
    return row?.actualQty ?? 0;
  }

  isLowStock(): boolean {
    const item = this.entity();
    if (!item?.reorderLevel || item.reorderLevel <= 0) return false;
    const totalStock = this.stockBalance().reduce((sum: number, b: any) => sum + (b.actualQty ?? 0), 0);
    return totalStock <= item.reorderLevel;
  }

  canTransfer(): boolean {
    return !!this.transferSourceWarehouse && !!this.transferTargetWarehouse
      && this.transferSourceWarehouse !== this.transferTargetWarehouse
      && this.transferQty > 0;
  }

  cancelTransfer(): void {
    this.showTransferForm.set(false);
    this.transferSourceWarehouse = '';
    this.transferTargetWarehouse = '';
    this.transferQty = 1;
  }

  submitTransfer(): void {
    if (!this.canTransfer()) return;
    this.isTransferring.set(true);
    const dto = {
      companyId: this.entity()?.companyId,
      entryType: 2, // MaterialTransfer
      postingDate: new Date().toISOString().substring(0, 10),
      items: [{
        itemId: this.entityId,
        quantity: this.transferQty,
        sourceWarehouseId: this.transferSourceWarehouse,
        targetWarehouseId: this.transferTargetWarehouse,
      }],
    };
    this.stockEntryService.create(dto as any).subscribe({
      next: () => {
        this.isTransferring.set(false);
        this.toaster.success(this.l.instant('::SuccessfullyCreated'));
        this.cancelTransfer();
        this.loadStockBalance();
        this.loadRecentMovements();
      },
      error: (err) => {
        this.isTransferring.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }
}
