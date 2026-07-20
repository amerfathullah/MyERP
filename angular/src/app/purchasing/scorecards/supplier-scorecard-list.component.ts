import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { SupplierScorecardService } from '../../proxy/purchasing/supplier-scorecard.service';

@Component({
  selector: 'app-supplier-scorecard-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SupplierScorecards' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/purchasing/scorecards/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewScorecard' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && scorecards.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-star-half-stroke fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoScorecardsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Supplier' | abpLocalization }}</th>
              <th>{{ 'Score' | abpLocalization }}</th>
              <th>{{ 'Standing' | abpLocalization }}</th>
              <th>{{ 'Period' | abpLocalization }}</th>
              <th>{{ 'Enforcement' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (s of scorecards; track s.id) {
                <tr>
                  <td><a [routerLink]="['/purchasing/scorecards', s.id]">{{ s.supplierId }}</a></td>
                  <td>
                    <span class="badge" [class]="getScoreBadge(s.score)">{{ s.score | number:'1.0-0' }}/100</span>
                  </td>
                  <td>{{ s.currentStanding ?? '—' }}</td>
                  <td>{{ s.periodType }}</td>
                  <td>
                    @if (hasEnforcement(s)) {
                      <span class="badge bg-danger">
                        @if (isPoBlocked(s)) { <i class="fa fa-ban me-1"></i>PO }
                        @if (isRfqBlocked(s)) { <i class="fa fa-ban me-1"></i>RFQ }
                      </span>
                    } @else {
                      <span class="badge bg-light text-dark">None</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class SupplierScorecardListComponent implements OnInit {
  private service = inject(SupplierScorecardService);
  scorecards: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.service.getList({ sorting: '', skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: res => { this.scorecards = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  getScoreBadge(score: number): string {
    if (score >= 70) return 'bg-success';
    if (score >= 40) return 'bg-warning';
    return 'bg-danger';
  }

  hasEnforcement(s: any): boolean {
    return s.standings?.some((st: any) => st.preventPos || st.preventRfqs && s.score >= st.minScore && s.score < st.maxScore);
  }

  isPoBlocked(s: any): boolean {
    return s.standings?.find((st: any) => s.score >= st.minScore && s.score < st.maxScore)?.preventPos ?? false;
  }

  isRfqBlocked(s: any): boolean {
    return s.standings?.find((st: any) => s.score >= st.minScore && s.score < st.maxScore)?.preventRfqs ?? false;
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
