import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoanService } from '../../proxy/human-resources/loan.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-loan-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe, AutoValidationDirective, SaveShortcutDirective],
  template: `
    <abp-page [title]="'NewLoan' | abpLocalization">
      <form [formGroup]="form" (appSaveShortcut)="save()">
        <div class="card mb-3"><div class="card-body">
          <h6 class="card-title">{{ 'LoanDetails' | abpLocalization }}</h6>
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'Employee' | abpLocalization }}</label>
              <select class="form-select" formControlName="employeeId">
                <option value="">{{ 'SelectEmployee' | abpLocalization }}</option>
                @for (emp of employees; track emp.id) {
                  <option [value]="emp.id">{{ emp.firstName }} {{ emp.lastName }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'LoanType' | abpLocalization }}</label>
              <select class="form-select" formControlName="loanType">
                <option [value]="0">Term Loan</option>
                <option [value]="1">Demand Loan</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'LoanAmount' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="loanAmount" step="0.01" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'InterestRate' | abpLocalization }} (%)</label>
              <input type="number" class="form-control" formControlName="interestRate" step="0.01" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'InterestMethod' | abpLocalization }}</label>
              <select class="form-select" formControlName="interestCalculationMethod">
                <option [value]="0">Diminishing Balance</option>
                <option [value]="1">Flat Rate</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'Tenure' | abpLocalization }} (months)</label>
              <input type="number" class="form-control" formControlName="repaymentPeriodMonths" min="1" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'RepaymentStartDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="repaymentStartDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'GracePeriod' | abpLocalization }} (months)</label>
              <input type="number" class="form-control" formControlName="gracePeriodMonths" min="0" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'PenaltyRate' | abpLocalization }} (%)</label>
              <input type="number" class="form-control" formControlName="penaltyInterestRate" step="0.01" />
            </div>
          </div>
        </div></div>

        <div class="d-flex justify-content-end gap-2">
          <button type="button" class="btn btn-outline-secondary" routerLink="/hr/loans">{{ 'Cancel' | abpLocalization }}</button>
          <button type="button" class="btn btn-primary" (click)="save()" [disabled]="!form.valid || saving">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `
})
export class LoanFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private loanService = inject(LoanService);
  private employeeService = inject(EmployeeService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  employees: any[] = [];
  saving = false;

  form = this.fb.group({
    employeeId: ['', Validators.required],
    loanType: [0],
    loanAmount: [0, [Validators.required, Validators.min(1)]],
    interestRate: [0, [Validators.required, Validators.min(0)]],
    interestCalculationMethod: [0],
    repaymentPeriodMonths: [12, [Validators.required, Validators.min(1)]],
    repaymentStartDate: ['', Validators.required],
    gracePeriodMonths: [0],
    penaltyInterestRate: [0]
  });

  ngOnInit() {
    this.employeeService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: res => { this.employees = res.items ?? []; }
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saving; }

  save() {
    if (!this.form.valid) return;
    this.saving = true;
    const val = this.form.getRawValue();
    const dto = {
      ...val,
      companyId: this.companyContext.currentCompanyId(),
      loanType: Number(val.loanType),
      interestCalculationMethod: Number(val.interestCalculationMethod)
    };
    this.loanService.create(dto as any).subscribe({
      next: created => {
        this.toaster.success('Loan created');
        this.router.navigate(['/hr/loans', created.id]);
      },
      error: () => { this.saving = false; }
    });
  }
}
