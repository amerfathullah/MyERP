import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { DesignationService } from '../../proxy/human-resources/designation.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface DesignationRow {
  id: string;
  name: string;
  description?: string | null;
}

/**
 * Employee designation/job title master. Per ERPNext: Designation (setup/doctype/designation).
 * Referenced by Employee.Designation by name.
 */
@Component({
  selector: 'app-designation-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-id-badge me-2"></i>{{ '::Designations' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (designations().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-id-badge fa-3x mb-2 d-block opacity-50"></i>
              <p>No designations configured.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (d of designations(); track d.id) {
                  <tr>
                    <td class="fw-medium">{{ d.name }}</td>
                    <td>{{ d.description }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editDesignation(d)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteDesignation(d.id)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditDesignation' | abpLocalization) : ('::NewDesignation' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="name" />
                </div>
                <div class="col-md-8">
                  <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                  <input class="form-control" formControlName="description" />
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
export class DesignationListComponent implements OnInit {
  private designationService = inject(DesignationService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  designations = signal<DesignationRow[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    name: ['', Validators.required],
    description: [''],
  });

  ngOnInit(): void {
    this.loadDesignations();
  }

  loadDesignations(): void {
    this.loading.set(true);
    this.designationService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.designations.set((res.items ?? []) as any); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', description: '' });
    this.showForm.set(true);
  }

  editDesignation(d: DesignationRow): void {
    this.editingId.set(d.id);
    this.form.patchValue({ name: d.name, description: d.description });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      name: this.form.value.name!,
      description: this.form.value.description || null,
    };

    const request$ = this.editingId()
      ? this.designationService.update(this.editingId()!, payload as any)
      : this.designationService.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadDesignations();
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

  deleteDesignation(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.designationService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadDesignations(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
