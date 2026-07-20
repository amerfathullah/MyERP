import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BudgetDetailService, type BudgetDto } from '../../shared/services/detail-services';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-budget-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <abp-page [title]="'Budgets' | abpLocalization">
  <app-breadcrumb />
      @if (budget) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-4"><strong>{{ 'BudgetAgainst' | abpLocalization }}:</strong> {{ budget.budgetAgainst }}</div>
            <div class="col-md-4"><strong>{{ 'Target' | abpLocalization }}:</strong> {{ budget.budgetAgainstName }}</div>
            <div class="col-md-4"><app-status-badge [status]="['Draft','Submitted','','','Cancelled'][budget.status ?? 0]"></app-status-badge></div>
          </div>
        </div></div>
        <div class="card"><div class="card-body">
          <h6>{{ 'Accounts' | abpLocalization }}</h6>
          <table class="table table-sm">
            <thead><tr><th>{{ 'AccountName' | abpLocalization }}</th><th class="text-end">{{ 'BudgetAmount' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (a of budget.accounts ?? []; track a.id) {
                <tr><td>{{ a.accountName ?? '—' }}</td><td class="text-end fw-bold">{{ a.budgetAmount | number:'1.2-2' }}</td></tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class BudgetDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(BudgetDetailService);
  budget: BudgetDto | null = null;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.budget = r);
  }
}
