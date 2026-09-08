import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { RepostItemValuationService } from '../../proxy/inventory/repost-item-valuation.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { RepostItemValuationDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-repost-item-valuation-list',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'RepostItemValuation' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'RepostItemValuation' | abpLocalization }}</h5>
          @if (pendingCount() > 0) {
            <span class="badge bg-warning">
              {{ pendingCount() }} {{ 'Pending' | abpLocalization }}
            </span>
          }
        </div>
        <div class="card-body">
          @if (loading()) {
            <div class="text-center py-4"><i class="fa fa-spinner fa-spin"></i></div>
          } @else if (entries().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-sync fa-3x mb-3 d-block"></i>
              <p>{{ 'NoRepostEntriesYet' | abpLocalization }}</p>
              <small class="text-muted">Repost entries are auto-created when backdated stock transactions are detected.</small>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'Method' | abpLocalization }}</th>
                  <th>{{ 'PostingDate' | abpLocalization }}</th>
                  <th>{{ 'Progress' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th>{{ 'GL' | abpLocalization }}</th>
                  <th>{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of entries(); track entry.id) {
                  <tr [class.table-danger]="entry.status === 3"
                      [class.table-warning]="entry.status === 1"
                      [class.table-secondary]="entry.isDeduplicated">
                    <td>{{ entry.creationTime | date:'dd/MM/yyyy HH:mm' }}</td>
                    <td>
                      <span class="badge" [class.bg-info]="entry.basedOn === 0"
                        [class.bg-primary]="entry.basedOn === 1"
                        [class.bg-dark]="entry.basedOn === 2">
                        {{ getMethodLabel(entry.basedOn ?? 0) }}
                      </span>
                    </td>
                    <td>{{ entry.postingDate | date:'dd/MM/yyyy' }}</td>
                    <td>
                      @if (entry.status === 1) {
                        <div class="progress" style="height: 18px;">
                          <div class="progress-bar progress-bar-striped progress-bar-animated"
                            [style.width.%]="getProgress(entry)">
                            {{ entry.currentIndex }}/{{ entry.totalAffectedEntries }}
                          </div>
                        </div>
                      } @else if (entry.status === 2) {
                        <span class="text-success">{{ entry.totalAffectedEntries }} entries</span>
                      } @else if (entry.status === 3) {
                        <span class="text-danger" [title]="entry.errorLog ?? ''">
                          <i class="fa fa-exclamation-triangle me-1"></i>Failed
                        </span>
                      } @else if (entry.isDeduplicated) {
                        <span class="text-muted"><i class="fa fa-link me-1"></i>Deduplicated</span>
                      } @else {
                        <span class="text-muted">Queued</span>
                      }
                    </td>
                    <td>
                      <span class="badge" [class.bg-secondary]="entry.status === 0"
                        [class.bg-warning]="entry.status === 1"
                        [class.bg-success]="entry.status === 2"
                        [class.bg-danger]="entry.status === 3"
                        [class.bg-dark]="entry.status === 4">
                        {{ getStatusLabel(entry.status ?? 0) }}
                      </span>
                    </td>
                    <td><i class="fa" [class.fa-check]="entry.repostGlEntries" [class.fa-minus]="!entry.repostGlEntries"
                        [class.text-success]="entry.repostGlEntries" [class.text-muted]="!entry.repostGlEntries"></i></td>
                    <td>
                      @if (entry.status === 3 || entry.status === 4 || entry.status === 5) {
                        <button type="button" class="btn btn-outline-primary btn-sm py-0 px-1 me-1"
                          [disabled]="busyId() === entry.id" (click)="restart(entry)" [title]="'::Restart' | abpLocalization">
                          <i class="fa fa-rotate-right"></i>
                        </button>
                      }
                      @if (entry.status === 0 || entry.status === 1 || entry.status === 3) {
                        <button type="button" class="btn btn-outline-danger btn-sm py-0 px-1"
                          [disabled]="busyId() === entry.id" (click)="cancel(entry)" [title]="'::Cancel' | abpLocalization">
                          <i class="fa fa-ban"></i>
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination [totalCount]="totalCount()" [pageSize]="20" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)" />
          }
        </div>
      </div>
    </abp-page>
  `
})
export class RepostItemValuationListComponent implements OnInit {
  private service = inject(RepostItemValuationService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  entries = signal<RepostItemValuationDto[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  pendingCount = signal(0);
  busyId = signal<string | null>(null);
  currentPage = 0;

  ngOnInit() {
    this.loadData();
    this.loadPendingCount();
  }

  loadData() {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    this.service.getList({ skipCount: this.currentPage * 20, maxResultCount: 20, companyId: companyId ?? undefined } as any).subscribe({
      next: res => {
        this.entries.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  loadPendingCount() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.service.getPendingCount(companyId).subscribe({
      next: count => this.pendingCount.set(count),
      error: () => {}
    });
  }

  getMethodLabel(basedOn: number): string {
    return ['Item + Warehouse', 'Item Wise', 'Entire Company'][basedOn] ?? 'Unknown';
  }

  private l = inject(LocalizationService);

  getStatusLabel(status: number): string {
    const keys = ['::Queued', '::InProcess', '::Completed', '::Failed', '::Skipped'];
    return keys[status] ? this.l.instant(keys[status]) : this.l.instant('::Unknown');
  }

  getProgress(entry: RepostItemValuationDto): number {
    return entry.totalAffectedEntries
      ? ((entry.currentIndex ?? 0) / entry.totalAffectedEntries) * 100 : 0;
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  /** Requeues a Failed/Skipped/Cancelled repost — the nightly worker picks it back up from Queued. */
  restart(entry: RepostItemValuationDto): void {
    if (!entry.id) return;
    this.busyId.set(entry.id);
    this.service.restart(entry.id).subscribe({
      next: () => { this.busyId.set(null); this.loadData(); this.loadPendingCount(); },
      error: (err: any) => { this.busyId.set(null); this.toaster.error(err?.error?.error?.message || '::OperationFailed'); },
    });
  }

  cancel(entry: RepostItemValuationDto): void {
    if (!entry.id) return;
    this.busyId.set(entry.id);
    this.service.cancel(entry.id).subscribe({
      next: () => { this.busyId.set(null); this.loadData(); this.loadPendingCount(); },
      error: (err: any) => { this.busyId.set(null); this.toaster.error(err?.error?.error?.message || '::OperationFailed'); },
    });
  }
}
