import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ShiftAssignmentService } from '../../proxy/human-resources/shift-assignment.service';
import { ShiftTypeService } from '../../proxy/human-resources/shift-type.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { ShiftAssignmentDto, ShiftTypeDto, EmployeeDto } from '../../proxy/human-resources/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

/**
 * Shift Assignment — assigns a Shift Type to an employee for a date range.
 * Per ERPNext: Shift Assignment (hr/doctype/shift_assignment).
 */
@Component({
  selector: 'app-shift-assignment-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-calendar-check me-2"></i>{{ '::ShiftAssignments' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (assignments().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-calendar-check fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoShiftAssignmentsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Employee' | abpLocalization }}</th>
                  <th>{{ '::ShiftType' | abpLocalization }}</th>
                  <th>{{ '::StartDate' | abpLocalization }}</th>
                  <th>{{ '::EndDate' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (a of assignments(); track a.id) {
                  <tr>
                    <td class="fw-medium">{{ a.employeeName }}</td>
                    <td>{{ a.shiftTypeName }}</td>
                    <td>{{ a.startDate | date:'mediumDate' }}</td>
                    <td>{{ a.endDate ? (a.endDate | date:'mediumDate') : '—' }}</td>
                    <td><span class="badge" [class]="a.status === 0 ? 'bg-success' : 'bg-secondary'">{{ a.status === 0 ? ('::Active' | abpLocalization) : ('::Inactive' | abpLocalization) }}</span></td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editAssignment(a)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteAssignment(a.id)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditShiftAssignment' | abpLocalization) : ('::NewShiftAssignment' | abpLocalization) }}</h6>
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
                  <label class="form-label">{{ '::ShiftType' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="shiftTypeId">
                    <option value="">—</option>
                    @for (s of shiftTypes(); track s.id) {
                      <option [value]="s.id">{{ s.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::StartDate' | abpLocalization }} *</label>
                  <input type="date" class="form-control" formControlName="startDate" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::EndDate' | abpLocalization }}</label>
                  <input type="date" class="form-control" formControlName="endDate" />
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
export class ShiftAssignmentListComponent implements OnInit {
  private service = inject(ShiftAssignmentService);
  private shiftTypeService = inject(ShiftTypeService);
  private employeeService = inject(EmployeeService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  assignments = signal<ShiftAssignmentDto[]>([]);
  shiftTypes = signal<ShiftTypeDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    employeeId: ['', Validators.required],
    shiftTypeId: ['', Validators.required],
    startDate: [new Date().toISOString().split('T')[0], Validators.required],
    endDate: [''],
  });

  ngOnInit(): void {
    this.loadAssignments();
    this.shiftTypeService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }).subscribe(r => this.shiftTypes.set(r.items ?? []));
    this.employeeService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' } as any).subscribe((r: any) => this.employees.set(r.items ?? []));
  }

  loadAssignments(): void {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.assignments.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ employeeId: '', shiftTypeId: '', startDate: new Date().toISOString().split('T')[0], endDate: '' });
    this.showForm.set(true);
  }

  editAssignment(a: ShiftAssignmentDto): void {
    this.editingId.set(a.id);
    this.form.patchValue({
      employeeId: a.employeeId,
      shiftTypeId: a.shiftTypeId,
      startDate: a.startDate?.substring(0, 10),
      endDate: a.endDate?.substring(0, 10) ?? '',
    });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      companyId: this.companyContext.currentCompanyId() ?? '',
      employeeId: this.form.value.employeeId!,
      shiftTypeId: this.form.value.shiftTypeId!,
      startDate: this.form.value.startDate!,
      endDate: this.form.value.endDate || null,
    };

    const request$ = this.editingId()
      ? this.service.update(this.editingId()!, payload)
      : this.service.create(payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadAssignments();
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

  deleteAssignment(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadAssignments(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
