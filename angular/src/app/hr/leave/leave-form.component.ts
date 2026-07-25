import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveService } from '../../proxy/human-resources/leave.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { LeaveTypeDto } from '../../proxy/human-resources/models';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CompanyService } from '../../proxy/core/company.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-leave-form',
  standalone: true,
  imports: [AutoValidationDirective, SaveShortcutDirective, CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ApplyLeave' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()" (appSaveShortcut)="save()">
        <div class="card mb-3">
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'Company' | abpLocalization }} *</label>
                <select class="form-select" formControlName="companyId" (change)="onCompanyChanged()">
                  <option value="">{{ 'SelectCompany' | abpLocalization }}</option>
                  @for (c of companies(); track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'Employee' | abpLocalization }} *</label>
                <select class="form-select" formControlName="employeeId">
                  <option value="">{{ 'SelectEmployee' | abpLocalization }}</option>
                  @for (e of employees(); track e.id) {
                    <option [value]="e.id">{{ e.firstName }} {{ e.lastName }}</option>
                  }
                </select>
              </div>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-4">
                <label class="form-label">{{ 'LeaveType' | abpLocalization }} *</label>
                <select class="form-select" formControlName="leaveTypeId">
                  <option value="">-- Select Leave Type --</option>
                  @for (lt of leaveTypes(); track lt.id) {
                    <option [value]="lt.id">{{ lt.name }} (max {{ lt.maxDaysAllowed }} days)</option>
                  }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'FromDate' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="fromDate">
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'ToDate' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="toDate">
              </div>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-3">
                <label class="form-label">{{ 'Days' | abpLocalization }} *</label>
                <input type="number" class="form-control" formControlName="totalLeaveDays" step="0.5" min="0.5">
              </div>
              <div class="col-md-3">
                <div class="form-check mt-4">
                  <input type="checkbox" class="form-check-input" id="halfDay" formControlName="halfDay">
                  <label class="form-check-label" for="halfDay">{{ 'HalfDay' | abpLocalization }}</label>
                </div>
              </div>
            </div>
            <div class="mt-3">
              <label class="form-label">{{ '::Reason' | abpLocalization }}</label>
              <textarea class="form-control" formControlName="reason" rows="3" maxlength="1000"></textarea>
            </div>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/hr/leave">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid">
            <i class="fa fa-paper-plane me-1"></i>{{ '::Submit' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class LeaveFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(LeaveService);
  private employeeService = inject(EmployeeService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  leaveTypes = signal<LeaveTypeDto[]>([]);
  companies = signal<{ id: string; name: string }[]>([]);
  employees = signal<{ id: string; firstName: string; lastName: string }[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    employeeId: ['', Validators.required],
    leaveTypeId: ['', Validators.required],
    fromDate: ['', Validators.required],
    toDate: ['', Validators.required],
    totalLeaveDays: [1, [Validators.required, Validators.min(0.5)]],
    halfDay: [false],
    reason: [''],
  });

  ngOnInit(): void {
    this.service.getLeaveTypes().subscribe(types => this.leaveTypes.set(types));
    this.companyService.getList({ maxResultCount: 200, skipCount: 0, sorting: '' }).subscribe(res => {
      this.companies.set((res.items ?? []).map((c: any) => ({ id: c.id, name: c.name })));
    });
    const cid = this.companyContext.currentCompanyId();
    if (cid) {
      this.form.patchValue({ companyId: cid });
      this.loadEmployees(cid);
    }
  }

  onCompanyChanged(): void {
    const cid = this.form.get('companyId')?.value;
    this.form.patchValue({ employeeId: '' });
    if (cid) this.loadEmployees(cid);
    else this.employees.set([]);
  }

  private loadEmployees(companyId: string): void {
    this.employeeService.getList({ skipCount: 0, maxResultCount: 200, sorting: '', companyId } as any).subscribe(res => {
      this.employees.set((res.items ?? []).map((e: any) => ({ id: e.id, firstName: e.firstName || '', lastName: e.lastName || '' })));
    });
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const leaveType = this.leaveTypes().find(t => t.id === val.leaveTypeId);
    this.service.apply({
      companyId: val.companyId,
      employeeId: val.employeeId,
      leaveTypeId: val.leaveTypeId,
      leaveTypeName: leaveType?.name,
      fromDate: val.fromDate,
      toDate: val.toDate,
      totalLeaveDays: val.totalLeaveDays,
      halfDay: val.halfDay,
      reason: val.reason || undefined,
    }).subscribe({
      next: () => { this.toaster.success('Leave application submitted'); this.router.navigate(['/hr/leave']); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}