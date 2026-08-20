import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ShiftTypeService } from '../../proxy/human-resources/shift-type.service';
import type { ShiftTypeDto } from '../../proxy/human-resources/models';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

/**
 * Shift Type master — defines shift timing (start/end) and rules.
 * Per ERPNext: Shift Type (hr/doctype/shift_type). Referenced by ShiftAssignment and Attendance.
 */
@Component({
  selector: 'app-shift-type-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-clock me-2"></i>{{ '::ShiftTypes' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (shiftTypes().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-clock fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoShiftTypesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::StartTime' | abpLocalization }}</th>
                  <th>{{ '::EndTime' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (s of shiftTypes(); track s.id) {
                  <tr>
                    <td class="fw-medium">{{ s.name }}</td>
                    <td>{{ s.startTime }}</td>
                    <td>{{ s.endTime }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editShiftType(s)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteShiftType(s.id!)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditShiftType' | abpLocalization) : ('::NewShiftType' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="name" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::StartTime' | abpLocalization }} *</label>
                  <input type="time" class="form-control" formControlName="startTime" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::EndTime' | abpLocalization }} *</label>
                  <input type="time" class="form-control" formControlName="endTime" />
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
export class ShiftTypeListComponent implements OnInit {
  private service = inject(ShiftTypeService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  shiftTypes = signal<ShiftTypeDto[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    name: ['', Validators.required],
    startTime: ['09:00', Validators.required],
    endTime: ['18:00', Validators.required],
  });

  ngOnInit(): void {
    this.loadShiftTypes();
  }

  loadShiftTypes(): void {
    this.loading.set(true);
    this.service.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.shiftTypes.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', startTime: '09:00', endTime: '18:00' });
    this.showForm.set(true);
  }

  editShiftType(s: ShiftTypeDto): void {
    this.editingId.set(s.id);
    this.form.patchValue({ name: s.name, startTime: s.startTime?.substring(0, 5), endTime: s.endTime?.substring(0, 5) });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      companyId: this.companyContext.currentCompanyId() ?? '',
      name: this.form.value.name!,
      startTime: this.form.value.startTime! + ':00',
      endTime: this.form.value.endTime! + ':00',
    };

    const request$ = this.editingId()
      ? this.service.update(this.editingId()!, payload)
      : this.service.create(payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadShiftTypes();
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

  deleteShiftType(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadShiftTypes(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
