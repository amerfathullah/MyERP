import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, RestService } from '@abp/ng.core';

@Component({
  selector: 'app-expense-claim-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewExpenseClaim' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Employee' | abpLocalization }}</label>
            <input class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.employeeName" placeholder="Employee name" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Type' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.expenseType">
              <option value="Travel">{{ 'Travel' | abpLocalization }}</option>
              <option value="Food">{{ 'Food' | abpLocalization }}</option>
              <option value="Accommodation">{{ 'Accommodation' | abpLocalization }}</option>
              <option value="Transportation">{{ 'Transportation' | abpLocalization }}</option>
              <option value="Other">{{ 'Other' | abpLocalization }}</option>
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Expenses' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr>
            <th>{{ 'Date' | abpLocalization }}</th>
            <th>{{ 'Description' | abpLocalization }}</th>
            <th>{{ 'Amount' | abpLocalization }}</th>
            <th></th>
          </tr></thead>
          <tbody>
            @for (exp of form.expenses; track $index) {
              <tr>
                <td><input type="date" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="exp.expenseDate" /></td>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="exp.description" /></td>
                <td><input type="number" class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="exp.amount" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="removeExpense($index)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addExpense()">
          <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-between">
          <span class="fw-bold">{{ 'Total' | abpLocalization }}: {{ getTotal() | number:'1.2-2' }}</span>
          <div class="d-flex gap-2">
            <button class="btn btn-secondary" routerLink="/hr/expense-claims">{{ 'Cancel' | abpLocalization }}</button>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving">
              <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
            </button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class ExpenseClaimFormComponent {
  private restService = inject(RestService);
  private router = inject(Router);
  saving = false;
  isDirty = false;
  form: any = { postingDate: new Date().toISOString().split('T')[0], expenseType: 'Travel', expenses: [{ expenseDate: '', description: '', amount: 0 }] };

  addExpense() { this.form.expenses.push({ expenseDate: '', description: '', amount: 0 }); }
  removeExpense(i: number) { this.form.expenses.splice(i, 1); }
  getTotal(): number { return this.form.expenses.reduce((s: number, e: any) => s + (e.amount || 0), 0); }

  save() {
    this.saving = true;
    this.restService.request({ method: 'POST', url: '/api/app/expense-claim', body: this.form }, { apiName: 'Default' })
      .subscribe({ next: () => { this.router.navigate(['/hr/expense-claims']); }, error: () => { this.saving = false;
  this.isDirty = false; } });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}