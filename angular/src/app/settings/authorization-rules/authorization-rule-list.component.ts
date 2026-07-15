import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-authorization-rule-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'AuthorizationRules' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/settings/authorization-rules/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewRule' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && rules.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-shield-halved fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">No authorization rules configured. Transactions will not require approval thresholds.</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'TransactionType' | abpLocalization }}</th>
              <th>Based On</th>
              <th>Threshold</th>
              <th>Approving Role</th>
              <th>Scope</th>
            </tr></thead>
            <tbody>
              @for (r of rules; track r.id) {
                <tr>
                  <td><span class="badge bg-primary">{{ r.transactionType }}</span></td>
                  <td>{{ r.basedOn }}</td>
                  <td class="fw-bold">{{ r.basedOn?.includes('Discount') ? r.thresholdValue + '%' : ('RM ' + r.thresholdValue) }}</td>
                  <td>{{ r.approvingRole ?? '—' }}</td>
                  <td>
                    @if (r.systemUserId) { <span class="badge bg-info">User-specific</span> }
                    @else if (r.systemRole) { <span class="badge bg-warning text-dark">Role: {{ r.systemRole }}</span> }
                    @else { <span class="badge bg-light text-dark">Global</span> }
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
export class AuthorizationRuleListComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);
  rules: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 50;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: String(this.currentPage * this.pageSize), maxResultCount: String(this.pageSize) };
    if (companyId) params.companyId = companyId;
    this.http.get<any>('/api/app/authorization-rule', { params }).subscribe({
      next: res => { this.rules = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
