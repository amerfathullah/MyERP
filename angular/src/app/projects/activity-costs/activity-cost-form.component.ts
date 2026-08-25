import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { ActivityCostService } from '../../proxy/projects/activity-cost.service';
import { ActivityTypeService } from '../../proxy/projects/activity-type.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { ActivityTypeDto } from '../../proxy/projects/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';

@Component({
  selector: 'app-activity-cost-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Activity Cost</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Employee *</label>
              <select class="form-select" formControlName="employeeId">
                <option value="">— Select Employee —</option>
                @for (e of employees(); track e.id) {
                  <option [value]="e.id">{{ e.employeeId ? e.employeeId + ' - ' : '' }}{{ e.fullName }}</option>
                }
              </select>
            </div>
            <div class="col-md-6">
              <label class="form-label">Activity Type *</label>
              <select class="form-select" formControlName="activityTypeId">
                <option value="">— Select Activity Type —</option>
                @for (a of activityTypes(); track a.id) {
                  <option [value]="a.id">{{ a.name }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Billing Rate (/hour) *</label>
              <input type="number" step="0.01" class="form-control" formControlName="billingRate" placeholder="0.00">
            </div>
            <div class="col-md-6">
              <label class="form-label">Costing Rate (/hour) *</label>
              <input type="number" step="0.01" class="form-control" formControlName="costingRate" placeholder="0.00">
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/projects/activity-costs" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ActivityCostFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ActivityCostService);
  private activityTypeService = inject(ActivityTypeService);
  private employeeService = inject(EmployeeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  activityTypes = signal<ActivityTypeDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      employeeId: ['', Validators.required],
      activityTypeId: ['', Validators.required],
      billingRate: [0, [Validators.required, Validators.min(0)]],
      costingRate: [0, [Validators.required, Validators.min(0)]],
    });
  }

  ngOnInit() {
    this.activityTypeService.getList().subscribe(r => {
      this.activityTypes.set(r ?? []);
    });

    this.employeeService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe(r => {
      this.employees.set(r.items ?? []);
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/projects/activity-costs']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
