import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BudgetService } from '../../proxy/accounting/budget.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { HttpClient } from '@angular/common/http';
@Component({
  selector: 'app-budget-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewBudget' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'FiscalYear' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.fiscalYearId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @for (fy of fiscalYears(); track fy.id) {
                <option [value]="fy.id">{{ fy.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'BudgetAgainst' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.budgetAgainst">
              <option value="CostCenter">{{ 'CostCenter' | abpLocalization }}</option>
              <option value="Project">Project</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'AnnualAction' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.actionIfAnnualBudgetExceeded">
              <option [ngValue]="0">Ignore</option>
              <option [ngValue]="1">Warn</option>
              <option [ngValue]="2">Stop</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'MonthlyAction' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.actionIfAccumulatedMonthlyBudgetExceeded">
              <option [ngValue]="0">Ignore</option>
              <option [ngValue]="1">Warn</option>
              <option [ngValue]="2">Stop</option>
            </select>
          </div>
        </div>
        <h6>{{ 'Accounts' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Account' | abpLocalization }}</th><th>{{ 'BudgetAmount' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (a of form.accounts; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="a.accountId">
                    <option value="">-- {{ 'SelectAccount' | abpLocalization }} --</option>
                    @for (acc of accounts(); track acc.id) {
                      <option [value]="acc.id">{{ acc.accountCode }} — {{ acc.accountName }}</option>
                    }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="100" (ngModelChange)="isDirty=true" [(ngModel)]="a.budgetAmount" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.accounts.splice($index,1); isDirty=true"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.accounts.push({accountId:'',budgetAmount:0}); isDirty=true"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>
        <div class="d-flex justify-content-end gap-2">
          <button class="btn btn-secondary" routerLink="/accounting/budgets">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class BudgetFormComponent implements OnInit {
  private budgetService = inject(BudgetService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private http = inject(HttpClient);

  saving = false;
  isDirty = false;
  accounts = signal<{ id: string; accountCode: string; accountName: string }[]>([]);
  fiscalYears = signal<{ id: string; name: string }[]>([]);

  form: any = {
    budgetAgainst: 'CostCenter', fiscalYearId: '',
    actionIfAnnualBudgetExceeded: 2, actionIfAccumulatedMonthlyBudgetExceeded: 1,
    accounts: [{ accountId: '', budgetAmount: 0 }]
  };

  ngOnInit(): void {
    this.http.get<any>('/api/app/account', { params: { maxResultCount: '500' } }).subscribe(r =>
      this.accounts.set((r.items ?? []).map((a: any) => ({ id: a.id, accountCode: a.accountCode, accountName: a.accountName })))
    );
    this.http.get<any>('/api/app/fiscal-year', { params: { maxResultCount: '50' } }).subscribe(r =>
      this.fiscalYears.set((r.items ?? []).map((fy: any) => ({ id: fy.id, name: fy.name ?? fy.fiscalYearName })))
    );
  }

  save() {
    this.saving = true;
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      fiscalYearId: this.form.fiscalYearId || undefined,
      budgetAgainst: this.form.budgetAgainst,
      budgetAgainstId: this.companyContext.currentCompanyId(), // uses company as default budget-against target
      actionIfAnnualBudgetExceeded: this.form.actionIfAnnualBudgetExceeded,
      actionIfAccumulatedMonthlyBudgetExceeded: this.form.actionIfAccumulatedMonthlyBudgetExceeded,
      accounts: this.form.accounts
        .filter((a: any) => a.accountId && a.budgetAmount > 0)
        .map((a: any) => ({ accountId: a.accountId, budgetAmount: a.budgetAmount }))
    };
    this.budgetService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/accounting/budgets']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}