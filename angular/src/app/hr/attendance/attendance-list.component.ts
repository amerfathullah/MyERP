import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { AttendanceService } from '../../proxy/human-resources/attendance.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { AttendanceDto, EmployeeDto } from '../../proxy/human-resources/models';
import { attendanceStatusOptions } from '../../proxy/human-resources/attendance-status.enum';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

/**
 * Employee Attendance — daily present/absent/half-day/on-leave record.
 * Per ERPNext: Attendance (hr/doctype/attendance).
 */
@Component({
  selector: 'app-attendance-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-calendar-day me-2"></i>{{ '::Attendance' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (records().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-calendar-day fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoAttendanceYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Employee' | abpLocalization }}</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (r of records(); track r.id) {
                  <tr>
                    <td class="fw-medium">{{ r.employeeName }}</td>
                    <td>{{ r.date | date:'mediumDate' }}</td>
                    <td><span class="badge" [class]="statusBadgeClass(r.status)">{{ statusLabel(r.status) }}</span></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editRecord(r)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteRecord(r.id)" title="Delete"><i class="fas fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>

      @if (showForm()) {
        <div class="card mt-3">
          <div class="card-header">
            <h6 class="mb-0">{{ editingId() ? ('::EditAttendance' | abpLocalization) : ('::NewAttendance' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Employee' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="employeeId">
                    <option value="">—</option>
                    @for (e of employees(); track e.id) {
                      <option [value]="e.id">{{ e.fullName }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::Date' | abpLocalization }} *</label>
                  <input type="date" class="form-control" formControlName="date" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::Status' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="status">
                    @for (o of statusOptions; track o.value) {
                      <option [value]="o.value">{{ o.key }}</option>
                    }
                  </select>
                </div>
              </div>

              <div class="d-flex gap-2">
                <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving()">
                  @if (saving()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
                <button type="button" class="btn btn-secondary" (click)="cancelForm()">{{ '::Cancel' | abpLocalization }}</button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
})
export class AttendanceListComponent implements OnInit {
  private service = inject(AttendanceService);
  private employeeService = inject(EmployeeService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  records = signal<AttendanceDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);
  statusOptions = attendanceStatusOptions;

  form = this.fb.group({
    employeeId: ['', Validators.required],
    date: [new Date().toISOString().split('T')[0], Validators.required],
    status: [0, Validators.required],
  });

  ngOnInit(): void {
    this.loadRecords();
    this.employeeService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' } as any).subscribe((r: any) => this.employees.set(r.items ?? []));
  }

  loadRecords(): void {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.records.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  statusLabel(status: number): string {
    return this.statusOptions.find(o => o.value === status)?.key ?? String(status);
  }

  statusBadgeClass(status: number): string {
    switch (status) {
      case 0: return 'bg-success';
      case 1: return 'bg-danger';
      case 2: return 'bg-warning text-dark';
      default: return 'bg-secondary';
    }
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ employeeId: '', date: new Date().toISOString().split('T')[0], status: 0 });
    this.showForm.set(true);
  }

  editRecord(r: AttendanceDto): void {
    this.editingId.set(r.id);
    this.form.patchValue({ employeeId: r.employeeId, date: r.date?.substring(0, 10), status: r.status });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      companyId: this.companyContext.currentCompanyId() ?? '',
      employeeId: this.form.value.employeeId!,
      date: this.form.value.date!,
      status: this.form.value.status!,
    };

    const request$ = this.editingId()
      ? this.service.update(this.editingId()!, payload)
      : this.service.create(payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadRecords();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.saving.set(false);
      },
    });
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  deleteRecord(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadRecords(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
