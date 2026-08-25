import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { TerritoryService } from '../../proxy/core/territory.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { TerritoryDto } from '../../proxy/core/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';

@Component({
  selector: 'app-territory-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Territory</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="mb-3">
            <label class="form-label">Territory Name *</label>
            <input type="text" class="form-control" formControlName="name" maxlength="140" placeholder="e.g. Malaysia, Southeast Asia, Selangor, Kuala Lumpur">
          </div>

          <div class="mb-3">
            <label class="form-label">Parent Territory</label>
            <select class="form-select" formControlName="parentId">
              <option [ngValue]="null">— None (Root Territory) —</option>
              @for (g of territories(); track g.id) {
                @if (g.id !== id) {
                  <option [value]="g.id">{{ g.name }}</option>
                }
              }
            </select>
          </div>

          <div class="mb-3">
            <div class="form-check form-switch">
              <input type="checkbox" class="form-check-input" formControlName="isGroup" id="isGroup">
              <label class="form-check-label" for="isGroup">Is Group (Region node — contains other territories)</label>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Territory Manager</label>
            <select class="form-select" formControlName="territoryManagerId">
              <option [ngValue]="null">— Select Territory Manager —</option>
              @for (e of employees(); track e.id) {
                <option [value]="e.id">{{ e.employeeId ? e.employeeId + ' - ' : '' }}{{ e.fullName }}</option>
              }
            </select>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/settings/territories" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class TerritoryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(TerritoryService);
  private employeeService = inject(EmployeeService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  territories = signal<TerritoryDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(140)]],
      parentId: [null],
      isGroup: [false],
      territoryManagerId: [null],
    });
  }

  ngOnInit() {
    this.service.getList({ isGroup: true, maxResultCount: 200 } as any).subscribe(r => {
      this.territories.set(r.items ?? []);
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
        this.router.navigate(['/settings/territories']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
