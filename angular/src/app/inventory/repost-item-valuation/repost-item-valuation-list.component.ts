import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface RepostEntry {
  id: string;
  companyId: string;
  basedOn: number;
  itemId: string | null;
  warehouseId: string | null;
  postingDate: string;
  status: number;
  repostGlEntries: boolean;
  totalAffectedEntries: number;
  currentIndex: number;
  errorLog: string | null;
  isDeduplicated: boolean;
  creationTime: string;
}

@Component({
  selector: 'app-repost-item-valuation-list',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, PaginationComponent],
  template: `
    <abp-page [title]="'RepostItemValuation' | abpLocalization">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ 'RepostItemValuation' | abpLocalization }}</h5>
          <span class="badge bg-warning" *ngIf="pendingCount() > 0">
            {{ pendingCount() }} {{ 'Pending' | abpLocalization }}
          </span>
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
                        {{ getMethodLabel(entry.basedOn) }}
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
                        {{ getStatusLabel(entry.status) }}
                      </span>
                    </td>
                    <td><i class="fa" [class.fa-check]="entry.repostGlEntries" [class.fa-minus]="!entry.repostGlEntries"
                        [class.text-success]="entry.repostGlEntries" [class.text-muted]="!entry.repostGlEntries"></i></td>
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
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  entries = signal<RepostEntry[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  pendingCount = signal(0);
  currentPage = 0;

  ngOnInit() {
    this.loadData();
    this.loadPendingCount();
  }

  loadData() {
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: this.currentPage * 20, maxResultCount: 20 };
    if (companyId) params.companyId = companyId;
    this.http.get<any>('/api/app/repost-item-valuation', { params }).subscribe({
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
    this.http.get<number>(`/api/app/repost-item-valuation/pending-count`, {
      params: { companyId }
    }).subscribe({
      next: count => this.pendingCount.set(count),
      error: () => {}
    });
  }

  getMethodLabel(basedOn: number): string {
    return ['Item + Warehouse', 'Item Wise', 'Entire Company'][basedOn] ?? 'Unknown';
  }

  getStatusLabel(status: number): string {
    return ['Queued', 'In Progress', 'Completed', 'Failed', 'Skipped'][status] ?? 'Unknown';
  }

  getProgress(entry: RepostEntry): number {
    return entry.totalAffectedEntries > 0
      ? (entry.currentIndex / entry.totalAffectedEntries) * 100 : 0;
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
