import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-asset-capitalization-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'AssetCapitalization' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/assets/capitalizations/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewCapitalization' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-building-circle-arrow-right fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">No asset capitalizations yet. Use this to convert CWIP to fixed assets.</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Target' | abpLocalization }} Asset</th>
              <th>{{ 'PostingDate' | abpLocalization }}</th>
              <th>Total Value</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (c of items; track c.id) {
                <tr>
                  <td>{{ c.targetAssetName ?? 'Unnamed Asset' }}</td>
                  <td>{{ c.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td class="fw-bold">RM {{ c.totalAssetValue | number:'1.2-2' }}</td>
                  <td><span class="badge" [class]="getStatusClass(c.status)">{{ c.status }}</span></td>
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
export class AssetCapitalizationListComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);
  items: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: String(this.currentPage * this.pageSize), maxResultCount: String(this.pageSize) };
    if (companyId) params.companyId = companyId;
    this.http.get<any>('/api/app/asset-capitalization', { params }).subscribe({
      next: res => { this.items = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  getStatusClass(status: string): string {
    switch (status) { case 'Submitted': return 'bg-success'; case 'Cancelled': return 'bg-danger'; default: return 'bg-secondary'; }
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
