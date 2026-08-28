import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DashboardService } from '../../proxy/core/dashboard.service';
import type { QuickReorderResultDto } from '../../proxy/core/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface ReorderItem {
  itemId: string;
  itemCode: string;
  itemName: string;
  reorderLevel: number;
  currentStock: number;
  projectedQty: number;
  shortageQty: number;
  selected: boolean;
}

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe],
  selector: 'app-stock-reorder',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="mb-0">{{ '::StockReorderRecommendations' | abpLocalization }}</h5>
          <small class="text-muted">{{ '::ItemsBelowReorderLevel' | abpLocalization }}</small>
        </div>
        <div class="d-flex gap-2">
          <button
            class="btn btn-outline-secondary btn-sm"
            [disabled]="loading()"
            (click)="loadItems()">
            <i class="fa fa-sync-alt" [class.fa-spin]="loading()"></i>
            {{ '::Refresh' | abpLocalization }}
          </button>
          <button
            class="btn btn-primary btn-sm"
            [disabled]="selectedCount() === 0 || isCreatingMR()"
            (click)="createMaterialRequest()">
            @if (isCreatingMR()) {
              <span class="spinner-border spinner-border-sm me-1"></span>
            } @else {
              <i class="fa fa-plus me-1"></i>
            }
            {{ '::CreateMaterialRequest' | abpLocalization }}
            @if (selectedCount() > 0) {
              ({{ selectedCount() }})
            }
          </button>
        </div>
      </div>

      <div class="card-body">
        <!-- Summary Stats -->
        @if (items().length > 0) {
          <div class="row g-3 mb-3">
            <div class="col-sm-4">
              <div class="p-3 bg-light rounded text-center">
                <div class="fs-4 fw-bold text-danger">{{ items().length }}</div>
                <small class="text-muted">{{ '::ItemsNeedingReorder' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-sm-4">
              <div class="p-3 bg-light rounded text-center">
                <div class="fs-4 fw-bold text-warning">{{ criticalCount() }}</div>
                <small class="text-muted">{{ '::CriticalShortage' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-sm-4">
              <div class="p-3 bg-light rounded text-center">
                <div class="fs-4 fw-bold text-primary">{{ totalShortage() | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::TotalUnitsShort' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        }

        <!-- Table of low stock items -->
        @if (loading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status"></div>
          </div>
        } @else if (items().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-check-circle text-success fs-1 mb-2 d-block"></i>
            <p class="mb-0">{{ '::AllStockLevelsHealthy' | abpLocalization }}</p>
            <small>{{ '::NoItemsBelowReorderLevel' | abpLocalization }}</small>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th style="width: 40px">
                    <input
                      type="checkbox"
                      class="form-check-input"
                      [checked]="allSelected()"
                      (change)="toggleSelectAll()" />
                  </th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReorderLevel' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ProjectedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Shortage' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.itemId) {
                  <tr [class.table-danger]="isCritical(item)" [class.table-warning]="!isCritical(item)">
                    <td>
                      <input
                        type="checkbox"
                        class="form-check-input"
                        [checked]="item.selected"
                        (change)="toggleItem(item)" />
                    </td>
                    <td>
                      <div class="fw-semibold">{{ item.itemName }}</div>
                      <small class="text-muted">{{ item.itemCode }}</small>
                    </td>
                    <td class="text-end">{{ item.reorderLevel | number:'1.0-2' }}</td>
                    <td class="text-end fw-semibold">{{ item.currentStock | number:'1.0-2' }}</td>
                    <td class="text-end" [class.text-danger]="item.projectedQty <= 0">
                      {{ item.projectedQty | number:'1.0-2' }}
                    </td>
                    <td class="text-end fw-bold text-danger">
                      {{ item.shortageQty | number:'1.0-2' }}
                    </td>
                    <td>
                      @if (item.currentStock <= 0) {
                        <span class="badge bg-danger">{{ '::OutOfStock' | abpLocalization }}</span>
                      } @else if (item.projectedQty <= 0) {
                        <span class="badge bg-danger">{{ '::ProjectedDeficit' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-warning text-dark">{{ '::BelowReorderLevel' | abpLocalization }}</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <!-- Success Message after MR creation -->
        @if (createdMR()) {
          <div class="alert alert-success mt-3 d-flex align-items-center justify-content-between" role="alert">
            <span>
              <i class="fa fa-check me-2"></i>
              {{ '::MaterialRequestCreatedSuccessfully' | abpLocalization }}:
              <a [routerLink]="['/purchasing/material-requests', createdMR()!.materialRequestId]" class="alert-link">
                {{ createdMR()!.materialRequestNumber }}
              </a>
              ({{ createdMR()!.itemCount }} {{ '::Items' | abpLocalization | lowercase }})
            </span>
          </div>
        }
      </div>
    </div>
  `,
})
export class StockReorderComponent implements OnInit {
  private dashboardService = inject(DashboardService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private companyContext = inject(CompanyContextService);

  items = signal<ReorderItem[]>([]);
  loading = signal(false);
  isCreatingMR = signal(false);
  createdMR = signal<QuickReorderResultDto | null>(null);

  ngOnInit(): void {
    this.loadItems();
  }

  selectedCount(): number {
    return this.items().filter(i => i.selected).length;
  }

  allSelected(): boolean {
    return this.items().length > 0 && this.items().every(i => i.selected);
  }

  criticalCount(): number {
    return this.items().filter(i => this.isCritical(i)).length;
  }

  totalShortage(): number {
    return this.items().reduce((sum, i) => sum + i.shortageQty, 0);
  }

  isCritical(item: ReorderItem): boolean {
    return item.projectedQty <= 0 || item.currentStock <= 0;
  }

  toggleSelectAll(): void {
    const newState = !this.allSelected();
    this.items.set(this.items().map(i => ({ ...i, selected: newState })));
  }

  toggleItem(item: ReorderItem): void {
    this.items.set(this.items().map(i =>
      i.itemId === item.itemId ? { ...i, selected: !i.selected } : i
    ));
  }

  clearSelection(): void {
    this.items.set(this.items().map(i => ({ ...i, selected: false })));
  }

  createMaterialRequest(): void {
    const selectedIds = this.items().filter(i => i.selected).map(i => i.itemId);
    if (selectedIds.length === 0) return;

    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) {
      this.toaster.warn(this.l.instant('::PleaseSelectCompanyFirst'));
      return;
    }

    this.isCreatingMR.set(true);
    this.createdMR.set(null);

    this.dashboardService.createReorderMaterialRequest({
      companyId,
      itemIds: selectedIds,
    }).subscribe({
      next: (result) => {
        this.isCreatingMR.set(false);
        this.createdMR.set(result);
        this.toaster.success(this.l.instant('::MaterialRequestCreatedForItems'));
        // Remove created items from the list
        this.items.set(this.items().filter(i => !selectedIds.includes(i.itemId)));
      },
      error: (err: any) => {
        this.isCreatingMR.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      }
    });
  }

  loadItems(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) {
      this.items.set([]);
      return;
    }

    this.loading.set(true);
    this.dashboardService.getLowStockItems(companyId).subscribe({
      next: (result) => {
        this.loading.set(false);
        this.items.set((result ?? []).map(i => ({
          itemId: i.itemId || '',
          itemCode: i.itemCode || '',
          itemName: i.itemName || '',
          reorderLevel: i.reorderLevel ?? 0,
          currentStock: i.currentStock ?? 0,
          projectedQty: i.projectedQty ?? 0,
          shortageQty: Math.max(0, (i.reorderLevel ?? 0) - (i.projectedQty ?? 0)),
          selected: false,
        })));
      },
      error: () => {
        this.loading.set(false);
        this.items.set([]);
      }
    });
  }
}
