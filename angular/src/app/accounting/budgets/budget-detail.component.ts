import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BudgetService } from '../../proxy/accounting/budget.service';
import type { BudgetDto } from '../../proxy/dtos/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-budget-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, ActivityLogComponent],
  template: `
    <abp-page [title]="'Budgets' | abpLocalization">
      <app-breadcrumb />
      @if (budget) {
        <div class="card mb-3"><div class="card-body">
          <div class="row align-items-center">
            <div class="col-md-3"><strong>{{ 'BudgetAgainst' | abpLocalization }}:</strong> {{ budget.budgetAgainst }}</div>
            <div class="col-md-3"><strong>{{ 'Target' | abpLocalization }}:</strong> {{ budget.budgetAgainstName }}</div>
            <div class="col-md-3"><app-status-badge [status]="['Draft','Submitted','','','Cancelled'][budget.status ?? 0]"></app-status-badge></div>
            <div class="col-md-3 text-end">
              @if ((budget.status ?? 0) === 0) {
                <button class="btn btn-sm btn-primary" (click)="submit()" [disabled]="loading()"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
              }
              @if ((budget.status ?? 0) === 1) {
                <button class="btn btn-sm btn-outline-danger" (click)="cancel()" [disabled]="loading()"><i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
              }
            </div>
          </div>
        </div></div>
        <div class="card mb-3"><div class="card-body">
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
        <app-activity-log documentType="Budget" [documentId]="budget.id!" />
      }
    </abp-page>
  `,
})
export class BudgetDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(BudgetService);
  private toaster = inject(ToasterService);
  budget: BudgetDto | null = null;
  loading = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.budget = r);
  }

  submit() {
    this.loading.set(true);
    this.service.submit(this.budget!.id!).subscribe({
      next: () => { this.toaster.success('Budget submitted'); this.reload(); },
      error: () => this.loading.set(false),
    });
  }

  cancel() {
    if (!confirm('Are you sure you want to cancel this budget?')) return;
    this.loading.set(true);
    this.service.cancel(this.budget!.id!).subscribe({
      next: () => { this.toaster.success('Budget cancelled'); this.reload(); },
      error: () => this.loading.set(false),
    });
  }

  private reload() {
    this.loading.set(false);
    this.service.get(this.budget!.id!).subscribe((r) => this.budget = r);
  }
}
