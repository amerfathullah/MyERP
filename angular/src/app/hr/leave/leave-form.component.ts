import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveService, LeaveTypeDto } from '../../proxy/hr/leave.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-leave-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ApplyLeave' | abpLocalization">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3">
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'Company' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="companyId">
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'Employee' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="employeeId">
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
                  <label class="form-check-label" for="halfDay">Half Day</label>
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
  private toaster = inject(ToasterService);

  leaveTypes = signal<LeaveTypeDto[]>([]);

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
}
