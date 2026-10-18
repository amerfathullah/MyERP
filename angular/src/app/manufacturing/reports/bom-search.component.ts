import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BomSearchService } from '../../proxy/manufacturing/bom-search.service';
import type { BomSearchReportDto, BomSearchRowDto } from '../../proxy/manufacturing/models';
import type { ItemDto } from '../../proxy/inventory/models';
import { ItemService } from '../../proxy/inventory/item.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { exportToCsv } from '../../shared/utils/csv-export';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  selector: 'app-bom-search',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">
            <i class="fa fa-search-plus me-2 text-primary"></i>
            {{ '::BomSearch' | abpLocalization }}
          </h5>
          <small class="text-muted">
            {{ '::BomSearchDescription' | abpLocalization }}
          </small>
        </div>
        <div>
          @if (filteredRows().length > 0) {
            <button class="btn btn-sm btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="card-body">
        <!-- Filter Controls -->
        <div class="p-3 bg-light rounded mb-4">
          <div class="row g-2 mb-2">
            <!-- Item 1 -->
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">Component Item 1</label>
              <select class="form-select form-select-sm" [(ngModel)]="item1Id">
                <option value="">-- Select Item 1 --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>

            <!-- Item 2 -->
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">Component Item 2</label>
              <select class="form-select form-select-sm" [(ngModel)]="item2Id">
                <option value="">-- Select Item 2 (Optional) --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>

            <!-- Item 3 -->
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">Component Item 3</label>
              <select class="form-select form-select-sm" [(ngModel)]="item3Id">
                <option value="">-- Select Item 3 (Optional) --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row g-2 mb-3 align-items-end">
            <!-- Item 4 -->
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">Component Item 4</label>
              <select class="form-select form-select-sm" [(ngModel)]="item4Id">
                <option value="">-- Select Item 4 (Optional) --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>

            <!-- Item 5 -->
            <div class="col-md-4 col-sm-6">
              <label class="form-label small fw-semibold">Component Item 5</label>
              <select class="form-select form-select-sm" [(ngModel)]="item5Id">
                <option value="">-- Select Item 5 (Optional) --</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
            </div>

            <!-- Sub Assemblies Toggle -->
            <div class="col-md-4 col-sm-12">
              <div class="form-check form-switch pt-4">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="subAssembliesSwitch"
                  [(ngModel)]="searchSubAssemblies"
                />
                <label class="form-check-label small fw-semibold" for="subAssembliesSwitch">
                  {{ '::SearchSubAssemblies' | abpLocalization }}
                </label>
                <div class="form-text text-muted small" style="font-size: 0.75rem;">
                  {{ '::SearchSubAssembliesHelp' | abpLocalization }}
                </div>
              </div>
            </div>
          </div>

          <div class="row g-2 align-items-center">
            <div class="col-md-6 col-sm-12">
              <input
                type="text"
                class="form-control form-control-sm"
                [placeholder]="'Filter search results by BOM #, FG item, component...'"
                [(ngModel)]="searchTerm"
              />
            </div>
            <div class="col-md-3 col-sm-6">
              <button
                class="btn btn-primary btn-sm w-100"
                (click)="loadData()"
                [disabled]="isLoading() || !hasSelectedAnyItem()"
              >
                <i class="fa fa-search me-1" [class.fa-spin]="isLoading()"></i>Search Assemblies
              </button>
            </div>
            <div class="col-md-3 col-sm-6">
              <button class="btn btn-outline-secondary btn-sm w-100" (click)="clearFilters()">
                <i class="fa fa-times me-1"></i>Reset
              </button>
            </div>
          </div>
        </div>

        <!-- KPI Metrics -->
        @if (reportData(); as data) {
          <div class="row g-3 mb-4">
            <div class="col-md-4 col-sm-12">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Total Matches</small>
                <span class="fs-4 fw-bold text-primary">{{ data.totalMatches }}</span>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">BOM Matches</small>
                <span class="fs-4 fw-bold text-success">{{ data.bomMatchesCount }}</span>
              </div>
            </div>
            <div class="col-md-4 col-sm-6">
              <div class="card border-0 bg-light p-3 text-center">
                <small class="text-muted fw-semibold">Product Bundle Matches</small>
                <span class="fs-4 fw-bold text-info">{{ data.productBundleMatchesCount }}</span>
              </div>
            </div>
          </div>
        }

        <!-- Results Table -->
        <div class="table-responsive border rounded">
          <table class="table table-sm table-hover align-middle mb-0">
            <thead class="table-light text-nowrap">
              <tr>
                <th>Type</th>
                <th>Assembly / BOM Number</th>
                <th>Finished Good / Parent Item</th>
                <th>Quantity</th>
                <th>Default</th>
                <th>Source Scope</th>
                <th class="text-end">Total Cost</th>
                <th>Contained Components</th>
              </tr>
            </thead>
            <tbody>
              @if (isLoading()) {
                <tr>
                  <td colspan="8" class="text-center py-4 text-muted">
                    <i class="fa fa-spinner fa-spin me-2"></i>Searching assemblies...
                  </td>
                </tr>
              } @else if (!hasSearched()) {
                <tr>
                  <td colspan="8" class="text-center py-4 text-muted">
                    <i class="fa fa-info-circle me-1"></i>Select at least one component item above and click "Search Assemblies".
                  </td>
                </tr>
              } @else if (filteredRows().length === 0) {
                <tr>
                  <td colspan="8" class="text-center py-4 text-muted">
                    <i class="fa fa-exclamation-circle me-1 text-warning"></i>
                    {{ '::NoMatchingBomsFound' | abpLocalization }}
                  </td>
                </tr>
              } @else {
                @for (row of filteredRows(); track row.parentId) {
                  <tr>
                    <td>
                      @if (row.docType === 'BOM') {
                        <span class="badge bg-primary">BOM</span>
                      } @else {
                        <span class="badge bg-info text-dark">Product Bundle</span>
                      }
                    </td>
                    <td>
                      <span class="fw-bold">{{ row.parentName }}</span>
                      @if (row.companyName) {
                        <div class="text-muted small">{{ row.companyName }}</div>
                      }
                    </td>
                    <td>
                      <div class="fw-semibold">{{ row.itemCode }}</div>
                      <div class="text-muted small text-truncate" style="max-width: 240px;">
                        {{ row.itemName }}
                      </div>
                    </td>
                    <td class="text-nowrap">
                      {{ row.quantity | number:'1.2-2' }} {{ row.uom }}
                    </td>
                    <td>
                      @if (row.isDefault) {
                        <span class="badge bg-success-subtle text-success border border-success-subtle">Default</span>
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td>
                      @if (row.foundInSubAssembly) {
                        <span class="badge bg-warning text-dark">
                          <i class="fa fa-layer-group me-1"></i>Via Sub-Assembly
                        </span>
                      } @else {
                        <span class="badge bg-light text-muted border">Direct Component</span>
                      }
                    </td>
                    <td class="text-end fw-semibold">
                      @if (row.totalCost !== null) {
                        {{ row.totalCost | currency:'USD':'symbol':'1.2-2' }}
                      } @else {
                        <span class="text-muted">-</span>
                      }
                    </td>
                    <td>
                      <small class="text-muted text-break" style="max-width: 320px; display: inline-block;">
                        {{ row.componentItemsSummary }}
                      </small>
                    </td>
                  </tr>
                }
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class BomSearchComponent implements OnInit {
  private bomSearchService = inject(BomSearchService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  reportData = signal<BomSearchReportDto | null>(null);
  items = signal<ItemDto[]>([]);
  isLoading = signal(false);
  hasSearched = signal(false);

  item1Id = '';
  item2Id = '';
  item3Id = '';
  item4Id = '';
  item5Id = '';
  searchSubAssemblies = false;
  searchTerm = '';

  hasSelectedAnyItem(): boolean {
    return !!(this.item1Id || this.item2Id || this.item3Id || this.item4Id || this.item5Id);
  }

  filteredRows = computed(() => {
    const data = this.reportData();
    if (!data?.rows) return [];

    const term = this.searchTerm.trim().toLowerCase();
    if (!term) return data.rows;

    return data.rows.filter(r =>
      r.parentName.toLowerCase().includes(term) ||
      r.itemCode.toLowerCase().includes(term) ||
      r.itemName.toLowerCase().includes(term) ||
      r.componentItemsSummary.toLowerCase().includes(term) ||
      (r.companyName && r.companyName.toLowerCase().includes(term))
    );
  });

  ngOnInit(): void {
    this.loadDropdowns();
  }

  private loadDropdowns(): void {
    this.itemService.getList({ maxResultCount: 1000 }).subscribe({
      next: res => this.items.set(res.items || []),
      error: () => {},
    });
  }

  loadData(): void {
    if (!this.hasSelectedAnyItem()) return;

    this.isLoading.set(true);
    this.hasSearched.set(true);
    const companyId = this.companyContext.selectedCompanyId() || undefined;

    this.bomSearchService
      .search({
        companyId,
        item1Id: this.item1Id || undefined,
        item2Id: this.item2Id || undefined,
        item3Id: this.item3Id || undefined,
        item4Id: this.item4Id || undefined,
        item5Id: this.item5Id || undefined,
        searchSubAssemblies: this.searchSubAssemblies,
        searchTerm: this.searchTerm.trim() || undefined,
      })
      .subscribe({
        next: res => {
          this.reportData.set(res);
          this.isLoading.set(false);
        },
        error: err => {
          this.isLoading.set(false);
          this.toaster.error(err.message || 'Failed to search assemblies');
        },
      });
  }

  clearFilters(): void {
    this.item1Id = '';
    this.item2Id = '';
    this.item3Id = '';
    this.item4Id = '';
    this.item5Id = '';
    this.searchSubAssemblies = false;
    this.searchTerm = '';
    this.reportData.set(null);
    this.hasSearched.set(false);
  }

  exportCsv(): void {
    const rows = this.filteredRows();
    if (!rows.length) return;

    const columns = [
      'DocType',
      'AssemblyNumber',
      'ItemCode',
      'ItemName',
      'Quantity',
      'Uom',
      'IsDefault',
      'SourceScope',
      'TotalCost',
      'Company',
      'ContainedComponents',
    ];

    const exportRows = rows.map(r => ({
      DocType: r.docType,
      AssemblyNumber: r.parentName,
      ItemCode: r.itemCode,
      ItemName: r.itemName,
      Quantity: r.quantity,
      Uom: r.uom || '',
      IsDefault: r.isDefault ? 'Yes' : 'No',
      SourceScope: r.foundInSubAssembly ? 'Via Sub-Assembly' : 'Direct Component',
      TotalCost: r.totalCost ?? '',
      Company: r.companyName || '',
      ContainedComponents: r.componentItemsSummary,
    }));

    exportToCsv('bom_search_results.csv', exportRows, columns);
  }
}
