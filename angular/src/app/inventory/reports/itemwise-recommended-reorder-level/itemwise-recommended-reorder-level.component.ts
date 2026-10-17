import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemwiseRecommendedReorderLevelService } from '../../../proxy/inventory/itemwise-recommended-reorder-level.service';
import type { ItemwiseRecommendedReorderLevelReportDto, ItemwiseRecommendedReorderLevelRowDto } from '../../../proxy/inventory/models';
import { ItemGroupService } from '../../../proxy/inventory/item-group.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-itemwise-recommended-reorder-level',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-calculator me-2 text-primary"></i>{{ '::ItemwiseRecommendedReorderLevel' | abpLocalization }}
          </h5>
          <small class="text-muted">{{ '::ItemwiseRecommendedReorderLevelDescription' | abpLocalization }}</small>
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
            <label class="form-label small fw-semibold">{{ '::ItemGroup' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="selectedItemGroupId" (change)="loadData()">
              <option value="">{{ '::AllItemGroups' | abpLocalization }}</option>
              @for (g of itemGroups(); track g.id) {
                <option [value]="g.id">{{ g.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm w-100" (click)="loadData()" [disabled]="isLoading()">
              <i class="fa fa-sync me-1" [class.fa-spin]="isLoading()"></i>{{ '::Generate' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Filter Sub-bar: Status Tabs and Search -->
        <div class="row g-2 mb-4 align-items-center">
          <div class="col-md-7">
            <div class="btn-group btn-group-sm" role="group">
              <button
                type="button"
                class="btn"
                [class.btn-primary]="statusFilter === 'all'"
                [class.btn-outline-primary]="statusFilter !== 'all'"
                (click)="setStatusFilter('all')"
              >
                All
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-danger]="statusFilter === 'UnderConfigured'"
                [class.btn-outline-danger]="statusFilter !== 'UnderConfigured'"
                (click)="setStatusFilter('UnderConfigured')"
              >
                {{ '::UnderConfigured' | abpLocalization }}
                @if (data()) {
                  ({{ data()!.underConfiguredCount }})
                }
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-warning]="statusFilter === 'OverConfigured'"
                [class.btn-outline-warning]="statusFilter !== 'OverConfigured'"
                (click)="setStatusFilter('OverConfigured')"
              >
                {{ '::OverConfigured' | abpLocalization }}
                @if (data()) {
                  ({{ data()!.overConfiguredCount }})
                }
              </button>
              <button
                type="button"
                class="btn"
                [class.btn-success]="statusFilter === 'Adequate'"
                [class.btn-outline-success]="statusFilter !== 'Adequate'"
                (click)="setStatusFilter('Adequate')"
              >
                {{ '::Adequate' | abpLocalization }}
              </button>
            </div>
          </div>
          <div class="col-md-5">
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
        </div>

        <!-- Summary KPI Cards -->
        @if (data() && data()!.rows && data()!.rows.length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::TotalItems' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-dark mt-1">
                    {{ data()!.totalItems }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::UnderConfigured' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-danger mt-1">
                    {{ data()!.underConfiguredCount }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::OverConfigured' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-warning mt-1">
                    {{ data()!.overConfiguredCount }}
                  </div>
                </div>
              </div>
            </div>
            <div class="col-md-3 col-sm-6">
              <div class="card bg-light border-0 shadow-sm h-100">
                <div class="card-body p-3">
                  <div class="text-muted small text-uppercase fw-semibold">{{ '::AvgDailyOutgoing' | abpLocalization }}</div>
                  <div class="h3 mb-0 text-info mt-1">
                    {{ data()!.totalAvgDailyOutgoing | number: '1.2-2' }}
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
                  <th style="min-width: 180px;">{{ '::Item' | abpLocalization }}</th>
                  <th style="min-width: 110px;">{{ '::ItemGroup' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::SafetyStock' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::LeadTimeDays' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::Consumed' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::Delivered' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::TotalOutgoing' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::AvgDailyOutgoing' | abpLocalization }}</th>
                  <th class="text-end" style="width: 110px;">{{ '::CurrentReorderLevel' | abpLocalization }}</th>
                  <th class="text-end" style="width: 120px;">{{ '::RecommendedReorderLevel' | abpLocalization }}</th>
                  <th class="text-end" style="width: 100px;">{{ '::Variance' | abpLocalization }}</th>
                  <th style="width: 120px;">{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of filteredRows(); track row.itemId) {
                  <tr>
                    <td>
                      <div class="fw-semibold text-primary font-monospace">{{ row.itemCode }}</div>
                      <div class="text-muted small text-truncate" style="max-width: 220px;">{{ row.itemName }}</div>
                    </td>
                    <td class="small text-muted">{{ row.itemGroup || '-' }}</td>
                    <td class="text-end font-monospace">{{ row.safetyStock | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.leadTimeDays }}d</td>
                    <td class="text-end font-monospace">{{ row.consumedQty | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.deliveredQty | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace fw-semibold">{{ row.totalOutgoing | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace">{{ row.avgDailyOutgoing | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace text-muted">{{ row.currentReorderLevel | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace fw-bold text-dark">{{ row.recommendedReorderLevel | number: '1.2-2' }}</td>
                    <td class="text-end font-monospace" [class.text-danger]="row.variance > 0" [class.text-warning]="row.variance < 0">
                      {{ row.variance > 0 ? '+' : '' }}{{ row.variance | number: '1.2-2' }}
                    </td>
                    <td>
                      @if (row.status === 'UnderConfigured') {
                        <span class="badge bg-danger">Under-configured</span>
                      } @else if (row.status === 'OverConfigured') {
                        <span class="badge bg-warning text-dark">Over-configured</span>
                      } @else {
                        <span class="badge bg-success">Adequate</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <!-- Empty State -->
        @if (!isLoading() && (!data() || filteredRows().length === 0)) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-boxes fa-3x mb-3 text-secondary"></i>
            <p class="mb-0">{{ '::NoDataAvailable' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>
  `,
})
export class ItemwiseRecommendedReorderLevelComponent implements OnInit {
  private reorderService = inject(ItemwiseRecommendedReorderLevelService);
  private itemGroupService = inject(ItemGroupService);
  private companyContext = inject(CompanyContextService);

  fromDate = '';
  toDate = '';
  selectedItemGroupId = '';
  searchTerm = '';
  statusFilter: 'all' | 'UnderConfigured' | 'OverConfigured' | 'Adequate' = 'all';

  itemGroups = signal<{ id: string; name: string }[]>([]);
  data = signal<ItemwiseRecommendedReorderLevelReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    const today = new Date();
    const thirtyDaysAgo = new Date();
    thirtyDaysAgo.setDate(today.getDate() - 30);

    this.fromDate = thirtyDaysAgo.toISOString().split('T')[0];
    this.toDate = today.toISOString().split('T')[0];

    this.loadItemGroups();
    this.loadData();
  }

  loadItemGroups(): void {
    this.itemGroupService.getList({ maxResultCount: 100 } as any).subscribe({
      next: (res) => {
        const list = res.items || [];
        this.itemGroups.set(list.map((g: any) => ({ id: g.id, name: g.name })));
      },
    });
  }

  loadData(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId || !this.fromDate || !this.toDate) return;

    this.isLoading.set(true);

    this.reorderService
      .getReport({
        companyId,
        fromDate: this.fromDate,
        toDate: this.toDate,
        itemGroupId: this.selectedItemGroupId || null,
        searchText: this.searchTerm || null,
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

  setStatusFilter(filter: 'all' | 'UnderConfigured' | 'OverConfigured' | 'Adequate'): void {
    this.statusFilter = filter;
  }

  filteredRows(): ItemwiseRecommendedReorderLevelRowDto[] {
    const report = this.data();
    if (!report || !report.rows) return [];

    let rows = report.rows;

    if (this.statusFilter !== 'all') {
      rows = rows.filter((r) => r.status === this.statusFilter);
    }

    const term = this.searchTerm.trim().toLowerCase();
    if (term) {
      rows = rows.filter(
        (r) =>
          r.itemCode.toLowerCase().includes(term) ||
          r.itemName.toLowerCase().includes(term) ||
          (r.itemGroup && r.itemGroup.toLowerCase().includes(term))
      );
    }

    return rows;
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows || rows.length === 0) return;

    const exportData = rows.map((r) => ({
      'Item Code': r.itemCode,
      'Item Name': r.itemName,
      'Item Group': r.itemGroup || '',
      'Safety Stock': r.safetyStock,
      'Lead Time Days': r.leadTimeDays,
      'Consumed Qty': r.consumedQty,
      'Delivered Qty': r.deliveredQty,
      'Total Outgoing': r.totalOutgoing,
      'Avg Daily Outgoing': r.avgDailyOutgoing,
      'Current Reorder Level': r.currentReorderLevel,
      'Recommended Reorder Level': r.recommendedReorderLevel,
      'Variance': r.variance,
      'Status': r.status,
    }));

    const columns = [
      'Item Code',
      'Item Name',
      'Item Group',
      'Safety Stock',
      'Lead Time Days',
      'Consumed Qty',
      'Delivered Qty',
      'Total Outgoing',
      'Avg Daily Outgoing',
      'Current Reorder Level',
      'Recommended Reorder Level',
      'Variance',
      'Status',
    ];

    exportToCsv(`recommended_reorder_level_${this.fromDate}_${this.toDate}.csv`, exportData, columns);
  }
}
