import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-budget-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewBudget' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'BudgetAgainst' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.budgetAgainst">
              <option value="CostCenter">Cost Center</option>
              <option value="Project">Project</option>
            </select>
          </div>
          <div class="col-md-8">
            <label class="form-label">{{ 'Target' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.budgetAgainstName" />
          </div>
        </div>
        <h6>{{ 'Accounts' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'AccountName' | abpLocalization }}</th><th>{{ 'BudgetAmount' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (a of form.accounts; track $index) {
              <tr>
                <td><input class="form-control form-control-sm" [(ngModel)]="a.accountName" /></td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="a.budgetAmount" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.accounts.splice($index,1)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="form.accounts.push({accountName:'',budgetAmount:0})"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>
        <div class="d-flex justify-content-end gap-2">
          <button class="btn btn-secondary" routerLink="/accounting/budgets">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class BudgetFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  form: any = { budgetAgainst: 'CostCenter', budgetAgainstName: '', accounts: [{ accountName: '', budgetAmount: 0 }] };
  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/budget', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => this.router.navigate(['/accounting/budgets']), error: () => { this.saving = false; } });
  }
}
