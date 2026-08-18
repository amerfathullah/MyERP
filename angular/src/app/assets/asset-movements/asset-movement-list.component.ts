import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { AssetMovementService } from '../../proxy/assets/asset-movement.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AssetMovementDto } from '../../proxy/assets/models';

const PURPOSE_LABELS: Record<number, string> = { 0: 'Issue', 1: 'Receipt', 2: 'Transfer', 3: 'Transfer & Issue' };
const STATUS_LABELS: Record<number, string> = { 0: 'Draft', 1: 'Submitted', 2: 'Cancelled' };

@Component({
  selector: 'app-asset-movement-list',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'AssetMovements' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'AssetMovements' | abpLocalization }}</h5>
          <a class="btn btn-primary btn-sm" routerLink="/assets/movements/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewMovement' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (isLoading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
          } @else if (items().length === 0) {
            <div class="text-center py-5">
              <i class="fa fa-exchange fa-3x text-muted mb-3 d-block"></i>
              <p class="text-muted">{{ 'NoAssetMovementsYet' | abpLocalization }}</p>
              <a class="btn btn-primary" routerLink="/assets/movements/new">
                <i class="fa fa-plus me-1"></i>{{ 'NewMovement' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'Asset' | abpLocalization }}</th>
                <th>{{ 'Purpose' | abpLocalization }}</th>
                <th>{{ 'Date' | abpLocalization }}</th>
                <th>{{ 'From' | abpLocalization }}</th>
                <th>{{ 'To' | abpLocalization }}</th>
                <th>{{ 'Status' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (m of items(); track m.id) {
                  <tr>
                    <td>{{ assetNames()[m.assetId ?? ''] || '—' }}</td>
                    <td>{{ purposeLabel(m.purpose) }}</td>
                    <td>{{ m.transactionDate ? (m.transactionDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td class="text-muted small">{{ m.sourceLocation || '—' }}</td>
                    <td class="text-muted small">{{ m.targetLocation || '—' }}</td>
                    <td>
                      <span class="badge"
                        [class.bg-secondary]="m.status === 0"
                        [class.bg-success]="m.status === 1"
                        [class.bg-danger]="m.status === 2">
                        {{ statusLabel(m.status) }}
                      </span>
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (m.status === 0) {
                          <button class="btn btn-outline-success" title="Submit" (click)="submitMovement(m)">
                            <i class="fa fa-check"></i>
                          </button>
                          <button class="btn btn-outline-danger" title="Delete" (click)="deleteMovement(m)">
                            <i class="fa fa-trash"></i>
                          </button>
                        }
                        @if (m.status === 1) {
                          <button class="btn btn-outline-danger" title="Cancel" (click)="cancelMovement(m)">
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
export class AssetMovementListComponent implements OnInit {
  private service = inject(AssetMovementService);
  private assetService = inject(AssetService);
  private companyContext = inject(CompanyContextService);
  private confirmation = inject(ConfirmationService);

  assetNames = signal<Record<string, string>>({});
  items = signal<AssetMovementDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.loadData();
    this.assetService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      const map: Record<string, string> = {};
      (r.items ?? []).forEach((a: any) => { map[a.id] = a.assetName ?? a.assetCode ?? a.id; });
      this.assetNames.set(map);
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    const cid = this.companyContext.currentCompanyId();
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, companyId: cid ?? undefined } as any).subscribe({
      next: r => { this.items.set(r.items ?? []); this.totalCount.set(r.totalCount); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  purposeLabel(purpose: any): string { return PURPOSE_LABELS[Number(purpose)] ?? String(purpose ?? '—'); }
  statusLabel(status: any): string { return STATUS_LABELS[Number(status)] ?? '—'; }

  submitMovement(m: AssetMovementDto): void {
    this.confirmation.warn('::SubmitConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.submit(m.id!).subscribe({ next: () => this.loadData() });
    });
  }

  cancelMovement(m: AssetMovementDto): void {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(m.id!).subscribe({ next: () => this.loadData() });
    });
  }

  deleteMovement(m: AssetMovementDto): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(m.id!).subscribe({ next: () => this.loadData() });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.loadData(); }
}
