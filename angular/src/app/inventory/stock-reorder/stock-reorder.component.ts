import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { HttpClient } from '@angular/common/http';
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
        <h5 class="mb-0"><i class="fa fa-boxes-stacked me-2"></i>{{ '::StockReorderManagement' | abpLocalization }}</h5>
        @if (items().length > 0) {
          <div class="btn-group btn-group-sm">
            <button class="btn btn-success" [disabled]="selectedCount() === 0 || isCreatingMR()"
                    (click)="createMaterialRequest()">
              @if (isCreatingMR()) {
                <span class="spinner-border spinner-border-sm me-1"></span>
              } @else {
                <i class="fa fa-file-circle-plus me-1"></i>
              }
              {{ '::CreateMaterialRequest' | abpLocalization }} ({{ selectedCount() }})
            </button>
          </div>
        }
      </div>
      <div class="card-body">
        <!-- KPI Summary -->
        @if (items().length > 0) {
          <div class="row g-3 mb-4">
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-4 fw-bold text-danger">{{ items().length }}</div>
                <small class="text-muted">{{ '::ItemsBelowReorder' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-4 fw-bold text-warning">{{ criticalCount() }}</div>
                <small class="text-muted">{{ '::CriticalItems' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-4 fw-bold text-primary">{{ totalShortage() | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::TotalShortageUnits' | abpLocalization }}</small>
              </div>
            </div>
            <div class="col-md-3">
              <div class="border rounded p-3 text-center">
                <div class="fs-4 fw-bold text-success">{{ selectedCount() }}</div>
                <small class="text-muted">{{ '::SelectedForReorder' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        }

        <!-- Selection Controls -->
        @if (items().length > 0) {
          <div class="d-flex justify-content-between align-items-center mb-3">
            <div class="form-check">
              <input type="checkbox" class="form-check-input" id="selectAll"
                     [checked]="allSelected()" (change)="toggleSelectAll()">
              <label class="form-check-label" for="selectAll">{{ '::SelectAll' | abpLocalization }}</label>
            </div>
            @if (selectedCount() > 0) {
              <button class="btn btn-outline-secondary btn-sm" (click)="clearSelection()">
                <i class="fa fa-times me-1"></i>{{ '::ClearSelection' | abpLocalization }}
              </button>
            }
          </div>
        }

        <!-- Items Table -->
        @if (loading()) {
          <div class="text-center py-5"><span class="spinner-border"></span></div>
        } @else if (items().length === 0) {
          <div class="text-center py-5 text-muted">
            <i class="fa fa-check-circle fa-3x mb-3 text-success"></i>
            <p class="fs-5">{{ '::AllStockLevelsAdequate' | abpLocalization }}</p>
            <p class="small">{{ '::NoItemsBelowReorderLevel' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover align-middle">
              <thead class="table-light">
                <tr>
                  <th style="width: 40px"></th>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReorderLevel' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CurrentStock' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ProjectedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Shortage' | abpLocalization }}</th>
                  <th>{{ '::Severity' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.itemId) {
                  <tr [class.table-danger]="isCritical(item)" [class.table-warning]="!isCritical(item)">
                    <td>
                      <input type="checkbox" class="form-check-input"
                             [checked]="item.selected" (change)="toggleItem(item)">
                    </td>
                    <td>
                      <a [routerLink]="['/inventory/items', item.itemId]" class="text-decoration-none">
                        <strong>{{ item.itemCode }}</strong>
                      </a>
                      <br><small class="text-muted">{{ item.itemName }}</small>
                    </td>
                    <td class="text-end">{{ item.reorderLevel | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.currentStock | number:'1.0-2' }}</td>
                    <td class="text-end" [class.text-danger]="item.projectedQty < 0">
                      {{ item.projectedQty | number:'1.0-2' }}
                    </td>
                    <td class="text-end fw-bold text-danger">{{ item.shortageQty | number:'1.0-0' }}</td>
                    <td>
                      @if (isCritical(item)) {
                        <span class="badge bg-danger">{{ '::Critical' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-warning text-dark">{{ '::Warning' | abpLocalization }}</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        <!-- Created MR Result -->
        @if (createdMR()) {
          <div class="alert alert-success d-flex align-items-center mt-3">
            <i class="fa fa-check-circle me-2"></i>
            <span>{{ '::MaterialRequestCreatedForItems' | abpLocalization }}:
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
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private companyContext = inject(CompanyContextService);

  items = signal<ReorderItem[]>([]);
  loading = signal(false);
  isCreatingMR = signal(false);
  createdMR = signal<{ materialRequestId: string; materialRequestNumber: string; itemCount: number } | null>(null);

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

    this.http.post<any>('/api/app/dashboard/create-reorder-material-request', {
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

  private loadItems(): void {
    this.loading.set(true);
    this.http.get<any[]>('/api/app/dashboard/low-stock-items').subscribe({
      next: (result) => {
        this.loading.set(false);
        this.items.set((result ?? []).map(i => ({
          itemId: i.itemId,
          itemCode: i.itemCode,
          itemName: i.itemName,
          reorderLevel: i.reorderLevel,
          currentStock: i.currentStock,
          projectedQty: i.projectedQty,
          shortageQty: Math.max(0, i.reorderLevel - i.projectedQty),
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
