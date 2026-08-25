import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { ShipmentParcelTemplateService } from '../../proxy/inventory/shipment-parcel-template.service';

@Component({
  selector: 'app-shipment-parcel-template-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Shipment Parcel Template</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-8">
              <label class="form-label">Parcel Template Name *</label>
              <input type="text" class="form-control" formControlName="parcelTemplateName" placeholder="e.g. Small Box, Medium Carton, Large Crate">
            </div>
            <div class="col-md-4 d-flex align-items-center mt-4">
              <div class="form-check form-switch">
                <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive">
                <label class="form-check-label" for="isActive">Active</label>
              </div>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-3">
              <label class="form-label">Length (cm) *</label>
              <input type="number" step="0.01" class="form-control" formControlName="length" placeholder="0.00">
            </div>
            <div class="col-md-3">
              <label class="form-label">Width (cm) *</label>
              <input type="number" step="0.01" class="form-control" formControlName="width" placeholder="0.00">
            </div>
            <div class="col-md-3">
              <label class="form-label">Height (cm) *</label>
              <input type="number" step="0.01" class="form-control" formControlName="height" placeholder="0.00">
            </div>
            <div class="col-md-3">
              <label class="form-label">Weight (kg) *</label>
              <input type="number" step="0.01" class="form-control" formControlName="weight" placeholder="0.00">
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" rows="3" formControlName="description" placeholder="Optional description / package specifications..."></textarea>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/inventory/shipment-parcel-templates" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ShipmentParcelTemplateFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ShipmentParcelTemplateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      parcelTemplateName: ['', [Validators.required, Validators.maxLength(140)]],
      length: [0, [Validators.required, Validators.min(0)]],
      width: [0, [Validators.required, Validators.min(0)]],
      height: [0, [Validators.required, Validators.min(0)]],
      weight: [0, [Validators.required, Validators.min(0)]],
      description: ['', Validators.maxLength(500)],
      isActive: [true],
    });
  }

  ngOnInit() {
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
        this.router.navigate(['/inventory/shipment-parcel-templates']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
