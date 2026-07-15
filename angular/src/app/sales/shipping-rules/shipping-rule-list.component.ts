import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-shipping-rule-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ShippingRules' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/sales/shipping-rules/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewShippingRule' | abpLocalization }}
        </button>
      </div>
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && rules.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-truck fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoShippingRulesYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Label' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th>{{ 'Mode' | abpLocalization }}</th>
              <th>{{ 'Amount' | abpLocalization }}</th>
              <th>{{ 'Countries' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (r of rules; track r.id) {
                <tr>
                  <td><a [routerLink]="['/sales/shipping-rules', r.id]">{{ r.label }}</a></td>
                  <td><span class="badge bg-info">{{ r.ruleType }}</span></td>
                  <td>{{ r.calculationMode }}</td>
                  <td>{{ r.shippingAmount > 0 ? ('RM ' + r.shippingAmount) : 'Tiered' }}</td>
                  <td>{{ r.countries?.length ? r.countries.join(', ') : 'Global' }}</td>
                  <td>
                    <span class="badge" [class]="r.isEnabled ? 'bg-success' : 'bg-secondary'">
                      {{ r.isEnabled ? 'Active' : 'Disabled' }}
                    </span>
                  </td>
                  <td>
                    <button class="btn btn-sm" [class]="r.isEnabled ? 'btn-outline-warning' : 'btn-outline-success'"
                      (click)="toggleRule(r)">
                      <i class="fa" [class]="r.isEnabled ? 'fa-pause' : 'fa-play'"></i>
                    </button>
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
export class ShippingRuleListComponent implements OnInit {
  private http = inject(HttpClient);
  rules: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.http.get<any>('/api/app/shipping-rule', {
      params: { skipCount: String(this.currentPage * this.pageSize), maxResultCount: String(this.pageSize) }
    }).subscribe({
      next: res => { this.rules = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  toggleRule(rule: any) {
    this.http.put<any>(`/api/app/shipping-rule/${rule.id}/toggle`, null, {
      params: { isEnabled: String(!rule.isEnabled) }
    }).subscribe({ next: () => { rule.isEnabled = !rule.isEnabled; } });
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
