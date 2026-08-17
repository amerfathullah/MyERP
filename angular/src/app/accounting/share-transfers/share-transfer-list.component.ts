import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ShareTransferService } from '../../proxy/accounting/share-transfer.service';
import { shareTransferTypeOptions } from '../../proxy/accounting/share-transfer-type.enum';
import type { ShareTransferDto } from '../../proxy/accounting/models';
import { ShareholderService } from '../../proxy/accounting/shareholder.service';

@Component({
  selector: 'app-share-transfer-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent, StatusBadgeComponent],
  template: `
    <abp-page [title]="'ShareTransfers' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/accounting/share-transfers/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewShareTransfer' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && transfers.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-right-left fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoShareTransfersYet' | abpLocalization }}</p>
          <button class="btn btn-primary" routerLink="/accounting/share-transfers/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewShareTransfer' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'TransferType' | abpLocalization }}</th>
                  <th>{{ 'FromShareholder' | abpLocalization }}</th>
                  <th>{{ 'ToShareholder' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Qty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (t of transfers; track t.id) {
                  <tr>
                    <td>{{ t.date | date:'dd/MM/yyyy' }}</td>
                    <td>{{ typeLabel(t.transferType) }}</td>
                    <td>{{ shareholderName(t.fromShareholderId) }}</td>
                    <td>{{ shareholderName(t.toShareholderId) }}</td>
                    <td class="text-end">{{ t.noOfShares }}</td>
                    <td class="text-end fw-bold">{{ t.amount | number:'1.2-2' }}</td>
                    <td><app-status-badge [status]="statusLabel(t.status)"></app-status-badge></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/accounting/share-transfers', t.id]">
                          <i class="fa fa-eye"></i>
                        </a>
                        @if ((t.status ?? 0) === 0) {
                          <button class="btn btn-outline-danger" (click)="remove(t)"><i class="fa fa-trash"></i></button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <app-pagination
        [totalCount]="totalCount"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class ShareTransferListComponent implements OnInit {
  private service = inject(ShareTransferService);
  private shareholderService = inject(ShareholderService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  transfers: ShareTransferDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  private typeLabels = shareTransferTypeOptions.reduce((acc, o) => ({ ...acc, [o.value as number]: o.key }), {} as Record<number, string>);
  private shareholders = new Map<string, string>();

  ngOnInit(): void {
    this.shareholderService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      (r.items ?? []).forEach(s => this.shareholders.set(s.id!, s.title!)));
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.transfers = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  typeLabel(t: number | undefined): string { return this.typeLabels[t ?? 0] ?? '—'; }
  shareholderName(id: string | null | undefined): string { return (id && this.shareholders.get(id)) ?? '—'; }
  statusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', '', '', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  remove(t: ShareTransferDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(t.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
