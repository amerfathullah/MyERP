import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemsToBeRequestedService } from '../../../proxy/inventory/items-to-be-requested.service';
import type { ItemsToBeRequestedReportDto, ItemsToBeRequestedItemDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-items-to-be-requested',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-clipboard-list me-2 text-danger"></i>{{ '::ItemsToBeRequested' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ItemsToBeRequestedDescription' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          @if (data() && data()!.items && data()!.items.length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
            <button class="btn btn-sm btn-outline-primary" (click)="createNewMaterialRequest()">
              <i class="fa fa-plus me-1"></i>{{ '::NewMaterialRequest' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-5 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Search' | abpLocalization }}</label>
            <input
              type="text"
              class="form-control form-control-sm"
              [placeholder]="'::Search' | abpLocalization"
              [(ngModel)]="searchTerm"
            />
          </div>
          <div class="col-md-3 col-sm-12">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.items && data()!.items.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::DeficitItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.totalDeficitItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalShortfallQty' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.totalShortfallQty | number: '1.2-4' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-12">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::EstimatedCost' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalEstimatedCost | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Data Table -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ '::Loading' | abpLocalization }}...</span>
            </div>
          </div>
        } @else if (!data() || filteredItems().length === 0) {
          <div class="text-center text-muted py-5">
            <i class="fa fa-boxes-stacked fa-3x mb-3 text-secondary"></i>
            <p>{{ '::NoItemsToBeRequestedFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive shadow-sm border rounded">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 220px;">{{ '::Item' | abpLocalization }}</th>
                  <th style="width: 140px;">{{ '::Warehouse' | abpLocalization }}</th>
                  <th style="width: 80px;">{{ '::UOM' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::ActualQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::RequestedQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::Ordered' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::Reserved' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::Projected' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::ShortfallQty' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::EstimatedCost' | abpLocalization }}</th>
                  <th style="width: 110px;">{{ '::RequestType' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of filteredItems(); track item.itemId + '-' + item.warehouseId) {
                  <tr>
                    <td>
                      <div class="fw-semibold">{{ item.itemCode }}</div>
                      <div class="small text-muted text-truncate" style="max-width: 200px;">{{ item.itemName }}</div>
                    </td>
                    <td>
                      <span class="small">{{ item.warehouseName }}</span>
                    </td>
                    <td>
                      <span class="small">{{ item.uom || '-' }}</span>
                    </td>
                    <td class="text-end font-monospace">
                      <span [class.text-danger]="item.actualQty <= 0">
                        {{ item.actualQty | number: '1.2-4' }}
                      </span>
                    </td>
                    <td class="text-end font-monospace text-muted">
                      {{ item.indentedQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end font-monospace text-muted">
                      {{ item.orderedQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end font-monospace text-muted">
                      {{ item.reservedQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end font-monospace">
                      <span class="badge bg-danger-subtle text-danger border border-danger-subtle px-2 py-1">
                        {{ item.projectedQty | number: '1.2-4' }}
                      </span>
                    </td>
                    <td class="text-end font-monospace fw-bold text-danger">
                      {{ item.shortfallQty | number: '1.2-4' }}
                    </td>
                    <td class="text-end font-monospace text-primary">
                      {{ item.estimatedShortfallCost | number: '1.2-2' }}
                    </td>
                    <td>
                      <span class="badge bg-secondary">{{ item.defaultMaterialRequestType || 'Purchase' }}</span>
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
export class ItemsToBeRequestedComponent implements OnInit {
  private readonly appService = inject(ItemsToBeRequestedService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly router = inject(Router);

  data = signal<ItemsToBeRequestedReportDto | null>(null);
  isLoading = signal<boolean>(false);
  warehouses = signal<{ id: string; name: string }[]>([]);

  selectedWarehouseId = '';
  searchTerm = '';

  filteredItems = computed(() => {
    const d = this.data();
    if (!d || !d.items) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return d.items;

    return d.items.filter(i =>
      (i.itemCode && i.itemCode.toLowerCase().includes(term)) ||
      (i.itemName && i.itemName.toLowerCase().includes(term)) ||
      (i.warehouseName && i.warehouseName.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadWarehouses();
    this.loadData();
  }

  loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.warehouseService.getList({
      maxResultCount: 200,
      skipCount: 0,
      companyId: companyId || undefined,
      isActive: true,
      isGroup: false
    } as any).subscribe({
      next: res => {
        this.warehouses.set((res.items ?? []).map(w => ({ id: w.id!, name: w.name! })));
      },
      error: () => this.warehouses.set([])
    });
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    this.isLoading.set(true);

    this.appService.getReport({
      companyId: companyId || undefined,
      warehouseId: this.selectedWarehouseId || undefined,
      searchTerm: this.searchTerm || undefined
    }).subscribe({
      next: result => {
        this.data.set(result);
        this.isLoading.set(false);
      },
      error: () => {
        this.data.set(null);
        this.isLoading.set(false);
      }
    });
  }

  createNewMaterialRequest(): void {
    this.router.navigate(['/purchasing/material-requests/new']);
  }

  exportCsv(): void {
    const items = this.filteredItems();
    if (!items || items.length === 0) return;

    const exportData = items.map(i => ({
      ItemCode: i.itemCode,
      ItemName: i.itemName,
      ItemGroup: i.itemGroup,
      Warehouse: i.warehouseName,
      UOM: i.uom || '',
      ActualQty: i.actualQty,
      RequestedQty: i.indentedQty,
      OrderedQty: i.orderedQty,
      ReservedQty: i.reservedQty,
      ProjectedQty: i.projectedQty,
      ShortfallQty: i.shortfallQty,
      StandardBuyingPrice: i.standardBuyingPrice,
      EstimatedShortfallCost: i.estimatedShortfallCost,
      RequestType: i.defaultMaterialRequestType
    }));

    const columns = [
      'ItemCode',
      'ItemName',
      'ItemGroup',
      'Warehouse',
      'UOM',
      'ActualQty',
      'RequestedQty',
      'OrderedQty',
      'ReservedQty',
      'ProjectedQty',
      'ShortfallQty',
      'StandardBuyingPrice',
      'EstimatedShortfallCost',
      'RequestType'
    ];

    exportToCsv('Items_To_Be_Requested_Report', exportData, columns);
  }
}
