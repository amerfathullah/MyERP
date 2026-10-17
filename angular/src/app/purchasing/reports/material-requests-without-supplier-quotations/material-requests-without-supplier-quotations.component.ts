import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { MaterialRequestsWithoutSupplierQuotationsService } from '../../../proxy/purchasing/material-requests-without-supplier-quotations.service';
import type { MaterialRequestsWithoutSupplierQuotationsReportDto, MaterialRequestsWithoutSupplierQuotationsItemDto } from '../../../proxy/purchasing/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-material-requests-without-supplier-quotations',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-file-circle-question me-2 text-warning"></i>{{ '::MaterialRequestsWithoutSupplierQuotations' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::MaterialRequestsWithoutSupplierQuotationsDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.items && data()!.items.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
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
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Search Bar -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-6">
            <div class="input-group input-group-sm">
              <span class="input-group-text"><i class="fa fa-search"></i></span>
              <input
                type="text"
                class="form-control"
                [placeholder]="'::Search' | abpLocalization"
                [(ngModel)]="searchTerm"
              />
            </div>
          </div>
          <div class="col-md-6 text-end text-muted small">
            @if (data()) {
              <span>{{ '::Showing' | abpLocalization }} {{ filteredItems().length }} {{ '::Entries' | abpLocalization }}</span>
            }
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.items && data()!.items.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::PendingRequests' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.totalPendingRequests }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::PendingItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.totalPendingItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4 col-sm-12">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalPendingQuantity' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalPendingQuantity | number: '1.2-4' }}
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
            <p>{{ '::NoMaterialRequestsWithoutSupplierQuotationsFound' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive shadow-sm border rounded">
            <table class="table table-sm table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 140px;">{{ '::MaterialRequest' | abpLocalization }}</th>
                  <th style="width: 110px;">{{ '::Date' | abpLocalization }}</th>
                  <th style="width: 110px;">{{ '::RequiredByDate' | abpLocalization }}</th>
                  <th style="min-width: 200px;">{{ '::Item' | abpLocalization }}</th>
                  <th style="width: 140px;">{{ '::Warehouse' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::Quantity' | abpLocalization }}</th>
                  <th style="width: 80px;">{{ '::UOM' | abpLocalization }}</th>
                  <th style="width: 130px;">{{ '::Action' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of filteredItems(); track item.materialRequestId + '-' + item.itemId) {
                  <tr>
                    <td>
                      <span class="fw-semibold text-primary font-monospace">{{ item.requestNumber }}</span>
                    </td>
                    <td>
                      <span class="small">{{ item.requestDate | date: 'mediumDate' }}</span>
                    </td>
                    <td>
                      <span class="small text-muted">{{ item.requiredByDate ? (item.requiredByDate | date: 'mediumDate') : '-' }}</span>
                    </td>
                    <td>
                      <div class="fw-semibold">{{ item.itemCode }}</div>
                      <div class="small text-muted text-truncate" style="max-width: 220px;">{{ item.itemName }}</div>
                    </td>
                    <td>
                      <span class="small">{{ item.warehouseName || '-' }}</span>
                    </td>
                    <td class="text-end font-monospace fw-semibold">
                      {{ item.quantity | number: '1.2-4' }}
                    </td>
                    <td>
                      <span class="small">{{ item.uom || '-' }}</span>
                    </td>
                    <td>
                      <button class="btn btn-xs btn-outline-primary" (click)="createSupplierQuotation(item)">
                        <i class="fa fa-file-lines me-1"></i>{{ '::Create' | abpLocalization }} SQ
                      </button>
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
export class MaterialRequestsWithoutSupplierQuotationsComponent implements OnInit {
  private readonly appService = inject(MaterialRequestsWithoutSupplierQuotationsService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly router = inject(Router);

  data = signal<MaterialRequestsWithoutSupplierQuotationsReportDto | null>(null);
  isLoading = signal<boolean>(false);
  warehouses = signal<{ id: string; name: string }[]>([]);

  fromDate = '';
  toDate = '';
  selectedWarehouseId = '';
  searchTerm = '';

  filteredItems = computed(() => {
    const d = this.data();
    if (!d || !d.items) return [];
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return d.items;

    return d.items.filter(i =>
      (i.requestNumber && i.requestNumber.toLowerCase().includes(term)) ||
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
      fromDate: this.fromDate || undefined,
      toDate: this.toDate || undefined,
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

  createSupplierQuotation(item: MaterialRequestsWithoutSupplierQuotationsItemDto): void {
    this.router.navigate(['/purchasing/supplier-quotations/new'], {
      queryParams: {
        materialRequestId: item.materialRequestId,
        itemId: item.itemId
      }
    });
  }

  exportCsv(): void {
    const items = this.filteredItems();
    if (!items || items.length === 0) return;

    const exportData = items.map(i => ({
      RequestNumber: i.requestNumber,
      Date: i.requestDate,
      RequiredByDate: i.requiredByDate || '',
      ItemCode: i.itemCode,
      ItemName: i.itemName,
      Description: i.description || '',
      Warehouse: i.warehouseName || '',
      Quantity: i.quantity,
      UOM: i.uom,
      Company: i.companyName
    }));

    const columns = [
      'RequestNumber',
      'Date',
      'RequiredByDate',
      'ItemCode',
      'ItemName',
      'Description',
      'Warehouse',
      'Quantity',
      'UOM',
      'Company'
    ];

    exportToCsv('Material_Requests_Without_Supplier_Quotations', exportData, columns);
  }
}
