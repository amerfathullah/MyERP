import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { RouterLink } from '@angular/router';
import { DashboardService } from '../../../proxy/core/dashboard.service';
import type { ReorderPointDashboardDto, ReorderPointItemDto } from '../../../proxy/core/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-reorder-point',
  standalone: true,
  imports: [CommonModule, LocalizationPipe, RouterLink, CompanyCurrencyPipe],
  template: `
    <div class="container-fluid">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h4><i class="fa fa-bell me-2 text-warning"></i>{{ '::ReorderPointDashboard' | abpLocalization }}</h4>
        <div class="btn-group">
          <button class="btn btn-outline-success" (click)="createReorderMRs()" [disabled]="selectedItems().length === 0 || isCreatingMR()">
            @if (isCreatingMR()) { <span class="spinner-border spinner-border-sm me-1"></span> }
            <i class="fa fa-cart-plus me-1"></i>{{ '::CreateMaterialRequests' | abpLocalization }}
            @if (selectedItems().length > 0) { <span class="badge bg-light text-dark ms-1">{{ selectedItems().length }}</span> }
          </button>
          <button class="btn btn-outline-primary" (click)="loadDashboard()" [disabled]="loading()">
            <i class="fa fa-refresh me-1"></i>{{ '::Refresh' | abpLocalization }}
          </button>
          <button class="btn btn-outline-secondary" (click)="exportCsv()" [disabled]="!dashboard() || (dashboard()?.items?.length ?? 0) === 0">
            <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        </div>
      </div>

      <!-- KPI Cards -->
      @if (dashboard(); as d) {
        <div class="row g-3 mb-4">
          <div class="col-12 col-md-3">
            <div class="card border-start border-danger border-4">
              <div class="card-body text-center py-3">
                <span class="fs-3 fw-bold text-danger">{{ d.totalItemsBelowReorder ?? 0 }}</span>
                <small class="text-muted d-block">{{ '::ItemsBelowReorder' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-12 col-md-3">
            <div class="card border-start border-warning border-4">
              <div class="card-body text-center py-3">
                <span class="fs-3 fw-bold text-warning">{{ criticalCount() }}</span>
                <small class="text-muted d-block">{{ '::CriticalItems' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-12 col-md-3">
            <div class="card border-start border-primary border-4">
              <div class="card-body text-center py-3">
                <span class="fs-3 fw-bold text-primary font-monospace">{{ "" | companyCurrency }} {{ d.totalShortageValue ?? 0 | number:'1.2-2' }}</span>
                <small class="text-muted d-block">{{ '::TotalShortageValue' | abpLocalization }}</small>
              </div>
            </div>
          </div>
          <div class="col-12 col-md-3">
            <div class="card border-start border-success border-4">
              <div class="card-body text-center py-3">
                <span class="fs-3 fw-bold text-success">{{ selectedItems().length }} / {{ d.items?.length ?? 0 }}</span>
                <small class="text-muted d-block">{{ '::SelectedForReorder' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Reorder Items Table -->
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <span class="fw-bold">{{ '::ItemsBelowReorderLevel' | abpLocalization }}</span>
          @if ((dashboard()?.items?.length ?? 0) > 0) {
            <div class="form-check">
              <input class="form-check-input" type="checkbox" id="selectAll" [checked]="allSelected()" (change)="toggleSelectAll()">
              <label class="form-check-label" for="selectAll">{{ '::SelectAll' | abpLocalization }}</label>
            </div>
          }
        </div>
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><span class="spinner-border text-primary"></span></div>
          } @else if (!dashboard() || (dashboard()?.items?.length ?? 0) === 0) {
            <div class="text-center py-5">
              <i class="fa fa-check-circle fa-3x text-success mb-3"></i>
              <p class="text-muted">{{ '::AllItemsAboveReorderLevel' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th style="width: 40px"></th>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ProjectedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReorderLevel' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ShortageQty' | abpLocalization }}</th>
                    <th>{{ '::Warehouse' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of dashboard()?.items ?? []; track item.itemId) {
                    <tr [class.table-danger]="isCritical(item)" [class.table-warning]="!isCritical(item)">
                      <td>
                        <input type="checkbox" class="form-check-input"
                          [checked]="item.itemId ? isSelected(item.itemId) : false"
                          (change)="item.itemId && toggleItem(item.itemId)">
                      </td>
                      <td>
                        @if (item.itemId) {
                          <a [routerLink]="['/inventory/items', item.itemId]" class="text-decoration-none fw-medium">{{ item.itemName }}</a>
                        } @else {
                          <span class="fw-medium">{{ item.itemName }}</span>
                        }
                        <br><small class="text-muted">{{ item.itemCode }}</small>
                      </td>
                      <td class="text-end font-monospace">{{ item.currentStock | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace" [class.text-danger]="(item.projectedQty ?? 0) < 0">{{ item.projectedQty | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace">{{ item.reorderLevel | number:'1.2-2' }}</td>
                      <td class="text-end font-monospace fw-bold text-danger">{{ item.shortageQty | number:'1.2-2' }}</td>
                      <td><small class="text-muted">{{ item.warehouseName || '—' }}</small></td>
                      <td>
                        @if (isCritical(item)) {
                          <span class="badge bg-danger"><i class="fa fa-exclamation-triangle me-1"></i>{{ '::Critical' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-warning text-dark"><i class="fa fa-clock me-1"></i>{{ '::Low' | abpLocalization }}</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
                <tfoot class="table-light">
                  <tr class="fw-bold">
                    <td></td>
                    <td>{{ '::Total' | abpLocalization }}</td>
                    <td></td><td></td><td></td>
                    <td class="text-end font-monospace text-danger">{{ totalShortage() | number:'1.2-2' }}</td>
                    <td></td>
                    <td></td>
                  </tr>
                </tfoot>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class ReorderPointComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  dashboard = signal<ReorderPointDashboardDto | null>(null);
  loading = signal(false);
  isCreatingMR = signal(false);
  private selected = signal<Set<string>>(new Set());

  selectedItems = computed(() => [...this.selected()]);
  allSelected = computed(() => {
    const d = this.dashboard();
    return d != null && (d.items?.length ?? 0) > 0 && this.selected().size === d.items!.length;
  });
  criticalCount = computed(() => this.dashboard()?.items?.filter(i => this.isCritical(i)).length ?? 0);
  totalShortage = computed(() => this.dashboard()?.items?.reduce((sum, i) => sum + (i.shortageQty ?? 0), 0) ?? 0);

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.loading.set(true);
    this.dashboardService.getReorderPointDashboard(companyId).subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.selected.set(new Set((data.items || []).map(i => i.itemId || '').filter(Boolean)));
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); }
    });
  }

  isCritical(item: ReorderPointItemDto): boolean {
    return (item.projectedQty ?? 0) <= 0 || (item.shortageQty ?? 0) > (item.reorderLevel ?? 0) * 0.5;
  }

  isSelected(itemId: string): boolean {
    return this.selected().has(itemId);
  }

  toggleItem(itemId: string): void {
    const s = new Set(this.selected());
    if (s.has(itemId)) s.delete(itemId); else s.add(itemId);
    this.selected.set(s);
  }

  toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selected.set(new Set());
    } else {
      this.selected.set(new Set((this.dashboard()?.items || []).map(i => i.itemId || '').filter(Boolean)));
    }
  }

  createReorderMRs(): void {
    const items = this.selectedItems();
    if (items.length === 0) return;
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isCreatingMR.set(true);
    this.dashboardService.createReorderMaterialRequest({ companyId, itemIds: items }).subscribe({
      next: (result) => {
        this.isCreatingMR.set(false);
        this.toaster.success(this.l.instant('::MaterialRequestsCreated', (result.itemCount ?? 1).toString()));
        this.loadDashboard();
      },
      error: (err: any) => {
        this.isCreatingMR.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }

  exportCsv(): void {
    if (!this.dashboard() || !this.dashboard()!.items) return;
    const columns = ['itemCode', 'itemName', 'currentStock', 'projectedQty', 'reorderLevel', 'shortageQty', 'warehouseName'];
    exportToCsv('reorder-point-report.csv', this.dashboard()!.items!, columns);
  }
}
