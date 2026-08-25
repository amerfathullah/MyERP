import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { PlantFloorService } from '../../proxy/manufacturing/plant-floor.service';
import { CompanyService } from '../../proxy/core/company.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyDto } from '../../proxy/core/models';
import { WarehouseDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-plant-floor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Plant Floor</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Floor Name *</label>
              <input type="text" class="form-control" formControlName="floorName" placeholder="e.g. Assembly Floor 1, Welding Bay">
            </div>
            <div class="col-md-6">
              <label class="form-label">Company *</label>
              <select class="form-select" formControlName="companyId">
                <option [ngValue]="null">Select Company...</option>
                @for (c of companies; track c.id) {
                  <option [ngValue]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Staging Warehouse</label>
              <select class="form-select" formControlName="warehouseId">
                <option [ngValue]="null">None</option>
                @for (w of warehouses; track w.id) {
                  <option [ngValue]="w.id">{{ w.name }}</option>
                }
              </select>
            </div>
            <div class="col-md-6 d-flex align-items-center mt-4">
              <div class="form-check form-switch">
                <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive">
                <label class="form-check-label" for="isActive">Active</label>
              </div>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" rows="3" formControlName="description" placeholder="Optional description..."></textarea>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/manufacturing/plant-floors" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class PlantFloorFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(PlantFloorService);
  private companyService = inject(CompanyService);
  private warehouseService = inject(WarehouseService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  companies: CompanyDto[] = [];
  warehouses: WarehouseDto[] = [];
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      floorName: ['', [Validators.required, Validators.maxLength(140)]],
      companyId: [null, Validators.required],
      warehouseId: [null],
      description: ['', Validators.maxLength(500)],
      isActive: [true],
    });
  }

  ngOnInit() {
    this.loadLookups();
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue(res);
      });
    }
  }

  private loadLookups() {
    this.companyService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe(res => {
      this.companies = res.items ?? [];
      if (!this.isEdit && this.companies.length > 0 && !this.form.get('companyId')?.value) {
        this.form.patchValue({ companyId: this.companies[0].id });
      }
    });

    this.warehouseService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.warehouses = res.items ?? [];
    });
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/manufacturing/plant-floors']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
