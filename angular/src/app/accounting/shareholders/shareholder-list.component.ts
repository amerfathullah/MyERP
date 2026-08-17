import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { ShareholderService } from '../../proxy/accounting/shareholder.service';
import type { ShareholderDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-shareholder-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent],
  template: `
    <abp-page [title]="'Shareholders' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <a class="btn btn-outline-secondary btn-sm" routerLink="/accounting/share-types">{{ 'ShareTypes' | abpLocalization }}</a>
        <a class="btn btn-outline-secondary btn-sm" routerLink="/accounting/share-transfers">{{ 'ShareTransfers' | abpLocalization }}</a>
        <button class="btn btn-primary btn-sm" routerLink="/accounting/shareholders/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewShareholder' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && shareholders.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-users fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoShareholdersYet' | abpLocalization }}</p>
          <button class="btn btn-primary" routerLink="/accounting/shareholders/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewShareholder' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Title' | abpLocalization }}</th>
                  <th>{{ 'FolioNo' | abpLocalization }}</th>
                  <th class="text-end">{{ 'TotalShares' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (s of shareholders; track s.id) {
                  <tr>
                    <td>{{ s.title }} @if (s.isCompany) { <span class="badge bg-secondary ms-1">{{ 'Company' | abpLocalization }}</span> }</td>
                    <td>{{ s.folioNo ?? '—' }}</td>
                    <td class="text-end fw-bold">{{ totalShares(s) }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/accounting/shareholders', s.id]">
                          <i class="fa fa-pen"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(s)">
                          <i class="fa fa-trash"></i>
                        </button>
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
export class ShareholderListComponent implements OnInit {
  private service = inject(ShareholderService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  shareholders: ShareholderDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.shareholders = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  totalShares(s: ShareholderDto): number {
    return (s.shareBalances ?? []).reduce((sum, b) => sum + (b.noOfShares ?? 0), 0);
  }

  remove(s: ShareholderDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(s.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
