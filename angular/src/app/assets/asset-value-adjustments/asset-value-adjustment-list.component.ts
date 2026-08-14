import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AssetValueAdjustmentService } from '../../proxy/assets/asset-value-adjustment.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetValueAdjustmentDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-value-adjustment-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetValueAdjustments' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetValueAdjustments' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/value-adjustments/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewAdjustment' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-balance-scale fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAdjustmentsYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/value-adjustments/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewAdjustment' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'Asset' | abpLocalization }}</th>
                <th>{{ 'Date' | abpLocalization }}</th>
                <th class="text-end">{{ 'CurrentValue' | abpLocalization }}</th>
                <th class="text-end">{{ 'NewValue' | abpLocalization }}</th>
                <th class="text-end">{{ 'Difference' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (adj of items(); track adj.id) {
                  <tr>
                    <td>{{ adj.assetName || adj.assetId || '—' }}</td>
                    <td>{{ adj.date ? (adj.date | date:'dd/MM/yyyy') : '—' }}</td>
                    <td class="text-end">{{ adj.currentAssetValue | number:'1.2-2' }}</td>
                    <td class="text-end fw-semibold">{{ adj.newAssetValue | number:'1.2-2' }}</td>
                    <td class="text-end"
                      [class.text-success]="(adj.differenceAmount ?? 0) > 0"
                      [class.text-danger]="(adj.differenceAmount ?? 0) < 0">
                      {{ adj.differenceAmount | number:'1.2-2' }}
                    </td>
                    <td>
                      <span class="badge"
                        [class.bg-secondary]="adj.status === 0"
                        [class.bg-success]="adj.status === 1"
                        [class.bg-danger]="adj.status === 2">
                        {{ adj.status === 0 ? 'Draft' : adj.status === 1 ? 'Submitted' : 'Cancelled' }}
                      </span>
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (adj.status === 0) {
                          <button class="btn btn-outline-success" title="Submit" (click)="submitAdj(adj)">
                            <i class="fa fa-check"></i>
                          </button>
                          <button class="btn btn-outline-danger" title="Delete" (click)="deleteAdj(adj)">
                            <i class="fa fa-trash"></i>
                          </button>
                        }
                        @if (adj.status === 1) {
                          <button class="btn btn-outline-warning" title="Cancel" (click)="cancelAdj(adj)">
                            <i class="fa fa-times"></i>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
      <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize"
        [currentPage]="currentPage" (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class AssetValueAdjustmentListComponent implements OnInit {
  private service = inject(AssetValueAdjustmentService);
  private companyContext = inject(CompanyContextService);
  private confirmation = inject(ConfirmationService);

  items = signal<AssetValueAdjustmentDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.loadData(); }

  loadData(): void {
    this.isLoading.set(true);
    const cid = this.companyContext.currentCompanyId();
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, companyId: cid ?? undefined } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  submitAdj(adj: AssetValueAdjustmentDto): void {
    this.confirmation.warn('::SubmitConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.submit(adj.id!).subscribe({ next: () => this.loadData() });
    });
  }

  cancelAdj(adj: AssetValueAdjustmentDto): void {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(adj.id!).subscribe({ next: () => this.loadData() });
    });
  }

  deleteAdj(adj: AssetValueAdjustmentDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(adj.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
