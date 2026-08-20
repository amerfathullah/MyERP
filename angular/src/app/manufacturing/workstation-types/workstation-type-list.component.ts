import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { WorkstationTypeService } from '../../proxy/manufacturing/workstation-type.service';
import type { WorkstationTypeDto } from '../../proxy/manufacturing/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

/**
 * Workstation Type master — a reusable operating-cost template shared by multiple
 * Workstations. Per ERPNext: manufacturing/doctype/workstation_type.
 */
@Component({
  selector: 'app-workstation-type-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-industry me-2"></i>{{ '::WorkstationTypes' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (types.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-industry fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoWorkstationTypesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::HourRate' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (t of types; track t.id) {
                  <tr>
                    <td class="fw-medium">{{ t.name }}</td>
                    <td>{{ t.hourRate | number:'1.2-2' }}</td>
                    <td>{{ t.description }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editType(t)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteType(t.id!)" title="Delete"><i class="fas fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>

      @if (showForm) {
        <div class="card mt-3">
          <div class="card-header">
            <h6 class="mb-0">{{ editingId ? ('::EditWorkstationType' | abpLocalization) : ('::NewWorkstationType' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-6">
                  <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="name" />
                </div>
                <div class="col-md-6">
                  <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                  <input class="form-control" formControlName="description" />
                </div>
              </div>

              <h6 class="mb-2">{{ '::CostComponents' | abpLocalization }}</h6>
              <table class="table table-sm">
                <thead><tr><th>{{ '::Component' | abpLocalization }}</th><th>{{ '::CostPerHour' | abpLocalization }}</th><th></th></tr></thead>
                <tbody formArrayName="costs">
                  @for (c of costs.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td><input class="form-control form-control-sm" formControlName="component" /></td>
                      <td><input type="number" class="form-control form-control-sm" formControlName="operatingCost" /></td>
                      <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="costs.removeAt($index)"><i class="fa fa-trash"></i></button></td>
                    </tr>
                  }
                </tbody>
              </table>
              <button type="button" class="btn btn-sm btn-outline-primary mb-3" (click)="addCost()">
                <i class="fa fa-plus me-1"></i>{{ '::AddItem' | abpLocalization }}
              </button>

              <div class="d-flex justify-content-between align-items-center">
                <span class="fw-bold">{{ '::HourRate' | abpLocalization }}: {{ totalHourRate() | number:'1.2-2' }}</span>
                <div class="d-flex gap-2">
                  <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving">
                    @if (saving) { <span class="spinner-border spinner-border-sm me-1"></span> }
                    <i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                  </button>
                  <button type="button" class="btn btn-secondary" (click)="cancelForm()">{{ '::Cancel' | abpLocalization }}</button>
                </div>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
})
export class WorkstationTypeListComponent implements OnInit {
  private service = inject(WorkstationTypeService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  types: WorkstationTypeDto[] = [];
  loading = true;
  showForm = false;
  saving = false;
  editingId: string | null = null;

  form = this.fb.group({
    name: ['', Validators.required],
    description: [''],
    costs: this.fb.array([]),
  });

  get costs(): FormArray<FormGroup> {
    return this.form.get('costs') as FormArray<FormGroup>;
  }

  ngOnInit(): void {
    this.loadTypes();
  }

  loadTypes(): void {
    this.loading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.types = res.items ?? []; this.loading = false; },
      error: () => this.loading = false,
    });
  }

  newCostGroup(component = '', operatingCost = 0): FormGroup {
    return this.fb.group({ component: [component, Validators.required], operatingCost: [operatingCost] });
  }

  addCost(): void {
    this.costs.push(this.newCostGroup());
  }

  totalHourRate(): number {
    return this.costs.controls.reduce((sum, c) => sum + (c.value.operatingCost || 0), 0);
  }

  openForm(): void {
    this.editingId = null;
    this.form.reset({ name: '', description: '' });
    this.costs.clear();
    this.addCost();
    this.showForm = true;
  }

  editType(t: WorkstationTypeDto): void {
    this.editingId = t.id!;
    this.form.patchValue({ name: t.name, description: t.description ?? '' });
    this.costs.clear();
    (t.costs ?? []).forEach(c => this.costs.push(this.newCostGroup(c.component, c.operatingCost)));
    if (this.costs.length === 0) this.addCost();
    this.showForm = true;
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const payload = {
      name: this.form.value.name!,
      description: this.form.value.description || null,
      costs: this.costs.value.filter((c: any) => c.component),
    };

    const request$ = this.editingId
      ? this.service.update(this.editingId, payload)
      : this.service.create(payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm = false;
        this.saving = false;
        this.loadTypes();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.saving = false;
      },
    });
  }

  cancelForm(): void {
    this.showForm = false;
  }

  deleteType(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadTypes(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
