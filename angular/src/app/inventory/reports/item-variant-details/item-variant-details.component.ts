import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemVariantDetailsService } from '../../../proxy/inventory/item-variant-details.service';
import type {
  ItemVariantDetailsReportDto,
  ItemVariantDetailsRowDto,
  WarehouseDto,
  ItemDto,
} from '../../../proxy/inventory/models';
import { ItemService } from '../../../proxy/inventory/item.service';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-item-variant-details',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-cubes me-2 text-primary"></i>{{ '::ItemVariantDetails' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ItemVariantDetailsDescription' | abpLocalization }}</small>
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
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::TemplateItem' | abpLocalization }} <span class="text-danger">*</span></label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedTemplateItemId" (change)="loadData()">
              <option value="">-- {{ '::Select' | abpLocalization }} {{ '::TemplateItem' | abpLocalization }} --</option>
              @for (item of templateItems(); track item.id) {
                <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
              }
            </select>
          </div>
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
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-2 col-sm-6">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading() || !selectedTemplateItemId">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (!selectedTemplateItemId) {
          <div class="alert alert-info py-3 text-center my-4">
            <i class="fa fa-info-circle me-2"></i>{{ '::Select' | abpLocalization }} {{ '::TemplateItem' | abpLocalization }}
          </div>
        } @else {
          <!-- Summary Cards -->
          @if (reportData()) {
            <div class="row g-3 mb-4">
              <div class="col-md-3 col-sm-6">
                <div class="card bg-light border-0 shadow-sm h-100">
                  <div class="card-body p-3">
                    <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalVariants' | abpLocalization }}</div>
                    <div class="h3 mb-0 text-primary mt-1">
                      {{ reportData()!.rows.length }}
                    </div>
                  </div>
                </div>
              </div>
              <div class="col-md-3 col-sm-6">
                <div class="card bg-light border-0 shadow-sm h-100">
                  <div class="card-body p-3">
                    <div class="text-muted small text-uppercase fw-semibold">{{ '::CurrentStock' | abpLocalization }}</div>
                    <div class="h3 mb-0 text-success mt-1">
                      {{ totalCurrentStock() | number: '1.2-2' }}
                    </div>
                  </div>
                </div>
              </div>
              <div class="col-md-3 col-sm-6">
                <div class="card bg-light border-0 shadow-sm h-100">
                  <div class="card-body p-3">
                    <div class="text-muted small text-uppercase fw-semibold">{{ '::InProduction' | abpLocalization }}</div>
                    <div class="h3 mb-0 text-info mt-1">
                      {{ totalInProduction() | number: '1.2-2' }}
                    </div>
                  </div>
                </div>
              </div>
              <div class="col-md-3 col-sm-6">
                <div class="card bg-light border-0 shadow-sm h-100">
                  <div class="card-body p-3">
                    <div class="text-muted small text-uppercase fw-semibold">{{ '::OpenSalesOrders' | abpLocalization }}</div>
                    <div class="h3 mb-0 text-warning mt-1">
                      {{ totalOpenOrders() }}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- Report Table -->
          @if (isLoading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">{{ '::Loading' | abpLocalization }}</span>
              </div>
            </div>
          } @else if (filteredRows().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-cubes fa-3x mb-3 text-secondary opacity-50"></i>
              <p class="mb-0">{{ '::NoItemVariantsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm table-hover table-striped align-middle border">
                <thead class="table-light">
                  <tr>
                    <th>#</th>
                    <th>{{ '::ItemCode' | abpLocalization }}</th>
                    <th>{{ '::ItemName' | abpLocalization }}</th>
                    @for (attr of reportData()!.attributeNames; track attr) {
                      <th>{{ attr }}</th>
                    }
                    <th class="text-end">{{ '::AvgBuyingPrice' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AvgSellingPrice' | abpLocalization }}</th>
                    <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                    <th class="text-end">{{ '::InProduction' | abpLocalization }}</th>
                    <th class="text-center">{{ '::OpenSalesOrders' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of filteredRows(); track row.variantId; let idx = $index) {
                    <tr>
                      <td class="text-muted small">{{ idx + 1 }}</td>
                      <td>
                        <span class="badge bg-secondary-subtle text-secondary fw-semibold">
                          {{ row.variantCode }}
                        </span>
                      </td>
                      <td class="fw-medium">{{ row.variantName }}</td>
                      @for (attr of reportData()!.attributeNames; track attr) {
                        <td>
                          @if (row.attributes[attr]) {
                            <span class="badge bg-light text-dark border">{{ row.attributes[attr] }}</span>
                          } @else {
                            <span class="text-muted">-</span>
                          }
                        </td>
                      }
                      <td class="text-end font-monospace">
                        {{ row.currency }} {{ row.avgBuyingPrice | number: '1.2-2' }}
                      </td>
                      <td class="text-end font-monospace">
                        {{ row.currency }} {{ row.avgSellingPrice | number: '1.2-2' }}
                      </td>
                      <td class="text-end">
                        <span
                          class="badge"
                          [class.bg-success-subtle]="row.currentStock > 0"
                          [class.text-success]="row.currentStock > 0"
                          [class.bg-light]="row.currentStock === 0"
                          [class.text-muted]="row.currentStock === 0"
                        >
                          {{ row.currentStock | number: '1.2-2' }}
                        </span>
                      </td>
                      <td class="text-end">
                        <span
                          class="badge"
                          [class.bg-info-subtle]="row.inProduction > 0"
                          [class.text-info]="row.inProduction > 0"
                          [class.bg-light]="row.inProduction === 0"
                          [class.text-muted]="row.inProduction === 0"
                        >
                          {{ row.inProduction | number: '1.2-2' }}
                        </span>
                      </td>
                      <td class="text-center">
                        <span
                          class="badge"
                          [class.bg-danger]="row.openOrders > 0"
                          [class.text-white]="row.openOrders > 0"
                          [class.bg-light]="row.openOrders === 0"
                          [class.text-muted]="row.openOrders === 0"
                        >
                          {{ row.openOrders }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class ItemVariantDetailsComponent implements OnInit {
  private itemVariantDetailsService = inject(ItemVariantDetailsService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);

  templateItems = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  reportData = signal<ItemVariantDetailsReportDto | null>(null);
  isLoading = signal(false);

  selectedTemplateItemId = '';
  selectedWarehouseId = '';
  searchTerm = '';

  totalCurrentStock = computed(() =>
    this.reportData()?.rows.reduce((sum, r) => sum + (r.currentStock || 0), 0) ?? 0
  );

  totalInProduction = computed(() =>
    this.reportData()?.rows.reduce((sum, r) => sum + (r.inProduction || 0), 0) ?? 0
  );

  totalOpenOrders = computed(() =>
    this.reportData()?.rows.reduce((sum, r) => sum + (r.openOrders || 0), 0) ?? 0
  );

  filteredRows = computed(() => {
    const data = this.reportData();
    if (!data || !data.rows) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return data.rows;

    return data.rows.filter(
      r =>
        r.variantCode.toLowerCase().includes(term) ||
        r.variantName.toLowerCase().includes(term) ||
        Object.values(r.attributes).some(v => v.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadDropdowns();
  }

  loadDropdowns(): void {
    // Fetch template items (hasVariants = true)
    this.itemService.getList({ hasVariants: true, maxResultCount: 500 } as any).subscribe({
      next: res => {
        this.templateItems.set(res.items ?? []);
      },
      error: () => {},
    });

    // Fetch active warehouses
    this.warehouseService.getList({ maxResultCount: 500 } as any).subscribe({
      next: res => {
        this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup && w.isActive));
      },
      error: () => {},
    });
  }

  loadData(): void {
    if (!this.selectedTemplateItemId) {
      this.reportData.set(null);
      return;
    }

    this.isLoading.set(true);
    this.itemVariantDetailsService
      .getReport({
        templateItemId: this.selectedTemplateItemId || null,
        warehouseId: this.selectedWarehouseId || null,
      })
      .subscribe({
        next: res => {
          this.reportData.set(res);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  exportCsv(): void {
    const data = this.reportData();
    if (!data || !data.rows.length) return;

    const exportRows = this.filteredRows().map(r => {
      const rowObj: Record<string, any> = {
        VariantCode: r.variantCode,
        VariantName: r.variantName,
      };
      data.attributeNames.forEach(attr => {
        rowObj[attr] = r.attributes[attr] || '';
      });
      rowObj['AvgBuyingPrice'] = r.avgBuyingPrice;
      rowObj['AvgSellingPrice'] = r.avgSellingPrice;
      rowObj['CurrentStock'] = r.currentStock;
      rowObj['InProduction'] = r.inProduction;
      rowObj['OpenSalesOrders'] = r.openOrders;
      rowObj['Currency'] = r.currency;
      return rowObj;
    });

    const columns = [
      'VariantCode',
      'VariantName',
      ...data.attributeNames,
      'AvgBuyingPrice',
      'AvgSellingPrice',
      'CurrentStock',
      'InProduction',
      'OpenSalesOrders',
      'Currency',
    ];

    const filename = `Item_Variant_Details_${data.templateItemCode || 'Report'}_${new Date().toISOString().slice(0, 10)}.csv`;
    exportToCsv(filename, exportRows, columns);
  }
}
