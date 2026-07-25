import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemStandardCostService } from '../../proxy/inventory/item-standard-cost.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { FormsModule } from '@angular/forms';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { ItemStandardCostDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-item-standard-cost-list',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, FormsModule,
    PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'ItemStandardCosts' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'ItemStandardCosts' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
            <i class="fa fa-plus me-1"></i>{{ 'New' | abpLocalization }}
          </button>
        </div>
        <div class="card-body">
          @if (showCreateForm) {
            <div class="card mb-3 border-primary">
              <div class="card-body">
                <div class="row g-2">
                  <div class="col-md-3">
                    <label class="form-label">{{ 'Item' | abpLocalization }}</label>
                    <select class="form-select form-select-sm" [(ngModel)]="newEntry.itemId">
                      <option value="">{{ 'SelectItem' | abpLocalization }}</option>
                      @for (item of items(); track item.id) {
                        <option [value]="item.id">{{ item.itemCode }}</option>
                      }
                    </select>
                  </div>
                  <div class="col-md-2">
                    <label class="form-label">{{ 'StandardRate' | abpLocalization }}</label>
                    <input type="number" class="form-control form-control-sm" [(ngModel)]="newEntry.standardRate" min="0.01" step="0.01">
                  </div>
                  <div class="col-md-2">
                    <label class="form-label">{{ 'EffectiveDate' | abpLocalization }}</label>
                    <input type="date" class="form-control form-control-sm" [(ngModel)]="newEntry.effectiveDate">
                  </div>
                  <div class="col-md-2 d-flex align-items-end">
                    <button class="btn btn-success btn-sm me-2" (click)="create()">
                      <i class="fa fa-check me-1"></i>{{ 'Save' | abpLocalization }}
                    </button>
                    <button class="btn btn-outline-secondary btn-sm" (click)="showCreateForm = false">
                      {{ 'Cancel' | abpLocalization }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          }

          @if (loading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin"></i></div>
          } @else if (entries().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-tag fa-3x mb-3 d-block"></i>
              <p>{{ 'NoStandardCostsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th>{{ 'StandardRate' | abpLocalization }}</th>
                  <th>{{ 'EffectiveDate' | abpLocalization }}</th>
                  <th>{{ 'PreviousRate' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th>{{ 'Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of entries(); track entry.id) {
                  <tr>
                    <td>{{ getItemName(entry.itemId) }}</td>
                    <td class="fw-bold">{{ entry.standardRate | number:'1.2-2' }}</td>
                    <td>{{ entry.effectiveDate | date:'dd/MM/yyyy' }}</td>
                    <td>
                      @if (entry.previousRate) {
                        <span class="text-muted">{{ entry.previousRate | number:'1.2-2' }}</span>
                        @if ((entry.standardRate ?? 0) > (entry.previousRate ?? 0)) {
                          <i class="fa fa-arrow-up text-danger ms-1"></i>
                        } @else {
                          <i class="fa fa-arrow-down text-success ms-1"></i>
                        }
                      } @else { — }
                    </td>
                    <td><app-status-badge [status]="getStatusLabel(entry.status ?? 0)" /></td>
                    <td>
                      @if (entry.status === 0) {
                        <button class="btn btn-outline-success btn-sm me-1" (click)="submit(entry.id!)">
                          {{ 'Submit' | abpLocalization }}
                        </button>
                      }
                      @if (entry.status === 1) {
                        <button class="btn btn-outline-danger btn-sm" (click)="cancel(entry.id!)">
                          {{ 'Cancel' | abpLocalization }}
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)" />
          }
        </div>
      </div>
    </abp-page>
  `
})
export class ItemStandardCostListComponent implements OnInit {
  private service = inject(ItemStandardCostService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);

  entries = signal<ItemStandardCostDto[]>([]);
  items = signal<{ id: string; itemCode: string }[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  currentPage = 0;
  showCreateForm = false;
  newEntry = { itemId: '', standardRate: 0, effectiveDate: '' };

  ngOnInit() {
    this.loadData();
    this.loadItems();
  }

  loadData() {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    this.service.getList({ skipCount: this.currentPage * 10, maxResultCount: 10, companyId: companyId ?? undefined } as any).subscribe({
      next: res => {
        this.entries.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadItems() {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe({
      next: res => this.items.set((res.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode ?? i.itemName })))
    });
  }

  create() {
    const companyId = this.companyContext.currentCompanyId();
    this.service.create({
      ...this.newEntry, companyId
    } as any).subscribe({
      next: () => { this.showCreateForm = false; this.loadData(); },
    });
  }

  submit(id: string) {
    this.service.submit(id).subscribe({
      next: () => this.loadData()
    });
  }

  cancel(id: string) {
    this.service.cancel(id).subscribe({
      next: () => this.loadData()
    });
  }

  getStatusLabel(status: number): string {
    return ['Draft', 'Submitted', 'Cancelled'][status] ?? 'Draft';
  }

  getItemName(itemId?: string): string {
    if (!itemId) return '—';
    const item = this.items().find(i => i.id === itemId);
    return item?.itemCode || itemId.substring(0, 8) + '…';
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
