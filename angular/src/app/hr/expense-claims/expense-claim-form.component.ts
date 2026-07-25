import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ExpenseClaimService } from '../../proxy/human-resources';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
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
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.employeeId">
              <option value="">-- {{ 'SelectEmployee' | abpLocalization }} --</option>
              @for (e of employees(); track e.id) {
                <option [value]="e.id">{{ e.name }}</option>
              }
            </select>
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
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="exp.amount" /></td>
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
export class ExpenseClaimFormComponent implements OnInit {
  private expenseClaimService = inject(ExpenseClaimService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private employeeService = inject(EmployeeService);

  saving = false;
  isDirty = false;
  employees = signal<{ id: string; name: string }[]>([]);
  form: any = {
    employeeId: '', postingDate: new Date().toISOString().split('T')[0],
    expenseType: 'Travel', expenses: [{ expenseDate: '', description: '', amount: 0 }]
  };

  ngOnInit(): void {
    this.employeeService.getList({ maxResultCount: 200 } as any).subscribe(r =>
      this.employees.set((r.items ?? []).map((e: any) => ({ id: e.id, name: `${e.firstName} ${e.lastName}`.trim() })))
    );
  }

  addExpense() { this.form.expenses.push({ expenseDate: '', description: '', amount: 0 }); this.isDirty = true; }
  removeExpense(i: number) { this.form.expenses.splice(i, 1); this.isDirty = true; }
  getTotal(): number { return this.form.expenses.reduce((s: number, e: any) => s + (e.amount || 0), 0); }

  save() {
    this.saving = true;
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      employeeId: this.form.employeeId || undefined,
      postingDate: this.form.postingDate,
      expenseType: this.form.expenseType,
      expenses: this.form.expenses
        .filter((e: any) => e.description)
        .map((e: any) => ({ expenseDate: e.expenseDate || this.form.postingDate, description: e.description, amount: e.amount || 0 }))
    };
    this.expenseClaimService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/hr/expense-claims']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}