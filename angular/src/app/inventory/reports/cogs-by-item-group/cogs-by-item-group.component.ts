import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CogsByItemGroupService } from '../../../proxy/inventory/cogs-by-item-group.service';
import type { CogsByItemGroupReportDto, CogsByItemGroupRowDto } from '../../../proxy/inventory/models';
import { WarehouseService } from '../../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-cogs-by-item-group',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-sitemap me-2 text-primary"></i>{{ '::CogsByItemGroup' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::CogsByItemGroupDescription' | abpLocalization }}</small>
        </div>
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <div class="card-body">
        <!-- Filters -->
        <div class="row g-2 mb-4 align-items-end">
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::FromDate' | abpLocalization }} *</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate" />
          </div>
          <div class="col-md-3 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::ToDate' | abpLocalization }} *</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate" />
          </div>
          <div class="col-md-4 col-sm-6">
            <label class="form-label small fw-semibold">{{ '::Warehouse' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedWarehouseId" (change)="loadData()">
              <option value="">{{ '::AllWarehouses' | abpLocalization }}</option>
              @for (w of warehouses(); track w.id) {
                <option [value]="w.id">{{ w.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter & Search Sub-bar -->
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
              <span>{{ '::Showing' | abpLocalization }} {{ filteredRows().length }} {{ '::Entries' | abpLocalization }}</span>
            }
          </div>
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalCogsDebit' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-primary mt-1">
                    {{ data()!.totalCogsDebit | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TopItemGroup' | abpLocalization }}</div>
                  <div class="h5 mb-0 text-dark mt-1 text-truncate">
                    {{ data()!.topItemGroupName || '-' }}
                  </div>
                  <div class="small text-muted">
                    {{ data()!.topItemGroupDebit | number: '1.2-2' }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-4">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItemGroups' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalItemGroups }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Loading State -->
        @if (isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">Loading...</span>
            </div>
            <div class="mt-2 text-muted small">{{ '::Loading' | abpLocalization }}...</div>
          </div>
        }

        <!-- Table State -->
        @if (!isLoading() && filteredRows().length > 0) {
          <div class="table-responsive">
            <table class="table table-hover table-bordered table-sm align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="min-width: 250px;">{{ '::ItemGroup' | abpLocalization }}</th>
                  <th class="text-end" style="width: 180px;">{{ '::CogsDebit' | abpLocalization }}</th>
                  <th style="width: 220px;">{{ '::PercentageOfTotal' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track $index) {
                  <tr [class.table-active]="row.isGroup && row.indent === 0">
                    <td>
                      <div class="d-flex align-items-center" [style.padding-left.px]="row.indent * 24">
                        @if (row.isGroup) {
                          <i class="fa fa-folder-open text-warning me-2"></i>
                          <span class="fw-bold">{{ row.itemGroupName }}</span>
                          <span class="badge bg-secondary ms-2 small">Group</span>
                        } @else {
                          <i class="fa fa-box text-secondary me-2"></i>
                          <span [class.fw-semibold]="row.indent === 0">{{ row.itemGroupName }}</span>
                        }
                      </div>
                    </td>
                    <td class="text-end font-monospace" [class.fw-bold]="row.isGroup">
                      {{ row.cogsDebit | number: '1.2-2' }}
                    </td>
                    <td>
                      <div class="d-flex align-items-center">
                        <div class="progress flex-grow-1 me-2" style="height: 6px;">
                          <div
                            class="progress-bar"
                            [class.bg-primary]="row.isGroup"
                            [class.bg-info]="!row.isGroup"
                            role="progressbar"
                            [style.width.%]="row.percentageOfTotal"
                          ></div>
                        </div>
                        <span class="small text-muted font-monospace" style="min-width: 45px;">
                          {{ row.percentageOfTotal | number: '1.2-2' }}%
                        </span>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light fw-bold">
                <tr>
                  <td>{{ '::Total' | abpLocalization }}</td>
                  <td class="text-end font-monospace">{{ data()!.totalCogsDebit | number: '1.2-2' }}</td>
                  <td>100.00%</td>
                </tr>
              </tfoot>
            </table>
          </div>
        }

        <!-- Empty State -->
        @if (!isLoading() && (!data() || filteredRows().length === 0)) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
            <p class="mb-0">{{ '::NoDataAvailable' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class CogsByItemGroupComponent implements OnInit {
  private cogsService = inject(CogsByItemGroupService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  selectedWarehouseId = '';
  searchTerm = '';

  warehouses = signal<{ id: string; name: string }[]>([]);
  data = signal<CogsByItemGroupReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    const today = new Date();
    const yearStart = new Date(today.getFullYear(), 0, 1);
    this.fromDate = yearStart.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadWarehouses();
    this.loadData();
  }

  loadWarehouses(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.warehouseService.getList({ maxResultCount: 100 } as any).subscribe({
      next: (res) => {
        const items = res.items || [];
        this.warehouses.set(
          items
            .filter((w: any) => !w.companyId || w.companyId === companyId)
            .map((w: any) => ({ id: w.id, name: w.warehouseName || w.name }))
        );
      },
    });
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);

    this.cogsService
      .getReport({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        warehouseId: this.selectedWarehouseId || null,
      })
      .subscribe({
        next: (result) => {
          this.data.set(result);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
  }

  filteredRows(): CogsByItemGroupRowDto[] {
    const report = this.data();
    if (!report || !report.rows) return [];

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return report.rows;

    return report.rows.filter((r) =>
      r.itemGroupName.toLowerCase().includes(term)
    );
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map((r) => ({
      'Item Group': '  '.repeat(r.indent) + r.itemGroupName,
      'Is Group': r.isGroup ? 'Yes' : 'No',
      'COGS Debit': r.cogsDebit,
      'Percentage (%)': r.percentageOfTotal,
    }));

    const columns = ['Item Group', 'Is Group', 'COGS Debit', 'Percentage (%)'];
    exportToCsv(`cogs_by_item_group_${this.fromDate}_${this.toDate}.csv`, exportData, columns);
  }
}
