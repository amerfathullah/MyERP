import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { TimesheetService } from '../../proxy/projects/timesheet.service';
import { ToasterService } from '@abp/ng.theme.shared';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-timesheet-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewTimesheet' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3">
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-4">
                <label class="form-label">{{ 'Company' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="companyId">
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'Employee' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="employeeId">
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'EmployeeName' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="employeeName">
              </div>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-4">
                <label class="form-label">{{ 'StartDate' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="startDate">
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'EndDate' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="endDate">
              </div>
            </div>
          </div>
        </div>

        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <span class="fw-bold">{{ 'TimeEntries' | abpLocalization }}</span>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addRow()">
              <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>{{ 'ActivityType' | abpLocalization }}</th>
                    <th>{{ 'TotalHours' | abpLocalization }}</th>
                    <th>{{ 'Billable' | abpLocalization }}</th>
                    <th>{{ 'BillingRate' | abpLocalization }}</th>
                    <th>{{ 'Description' | abpLocalization }}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody formArrayName="details">
                  @for (row of details.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td><input type="text" class="form-control form-control-sm" formControlName="activityType"></td>
                      <td><input type="number" class="form-control form-control-sm text-end" formControlName="hours" step="0.5"></td>
                      <td class="text-center"><input type="checkbox" class="form-check-input" formControlName="isBillable"></td>
                      <td><input type="number" class="form-control form-control-sm text-end" formControlName="billingRate" step="1"></td>
                      <td><input type="text" class="form-control form-control-sm" formControlName="description"></td>
                      <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeRow(i)"><i class="fa fa-trash"></i></button></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/projects/timesheets">{{ 'Cancel' | abpLocalization }}</a>
          <button type="submit" class="btn btn-primary" [disabled]="form.invalid || details.length === 0">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </abp-page>
  `,
})
export class TimesheetFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(TimesheetService);
  private toaster = inject(ToasterService);

  form = this.fb.group({
    companyId: ['', Validators.required],
    employeeId: ['', Validators.required],
    employeeName: [''],
    startDate: [new Date().toISOString().split('T')[0], Validators.required],
    endDate: [new Date().toISOString().split('T')[0], Validators.required],
    details: this.fb.array([]),
  });

  get details(): FormArray { return this.form.get('details') as FormArray; }

  addRow(): void {
    const now = new Date().toISOString();
    this.details.push(this.fb.group({
      activityType: ['Development', Validators.required],
      fromTime: [now],
      toTime: [now],
      hours: [8, [Validators.required, Validators.min(0.1)]],
      isBillable: [true],
      billingRate: [0],
      costingRate: [0],
      description: [''],
    }));
  }

  removeRow(index: number): void { this.details.removeAt(index); }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    this.service.create({
      companyId: val.companyId,
      employeeId: val.employeeId,
      employeeName: val.employeeName,
      startDate: val.startDate,
      endDate: val.endDate,
      details: (val.details ?? []).map((d: any) => ({
        activityType: d.activityType,
        fromTime: d.fromTime,
        toTime: d.toTime,
        hours: d.hours,
        isBillable: d.isBillable,
        billingRate: d.billingRate,
        costingRate: d.costingRate,
        description: d.description,
      })),
    }).subscribe({
      next: () => { this.toaster.success('Timesheet created'); this.router.navigate(['/projects/timesheets']); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}