import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-bom-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="isEditMode ? 'EditBOM' : ('NewBOM' | abpLocalization)">
      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'Item' | abpLocalization }} *</label>
                <input type="text" class="form-control" formControlName="itemId" placeholder="Item ID" />
              </div>
              <div class="col-md-4 mb-3">
                <label class="form-label">{{ 'ItemName' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="itemName" />
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'Quantity' | abpLocalization }} *</label>
                <input type="number" class="form-control" formControlName="quantity" min="0.01" step="0.01" />
              </div>
              <div class="col-md-2 mb-3">
                <label class="form-label">{{ 'IsActive' | abpLocalization }}</label>
                <div class="form-check mt-2">
                  <input type="checkbox" class="form-check-input" formControlName="isActive" id="bomActive" />
                  <label class="form-check-label" for="bomActive">Active</label>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="card mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="card-title mb-0"><i class="fa fa-list me-2"></i>{{ 'Materials' | abpLocalization }}</h6>
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="addMaterial()">
              <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table table-sm mb-0">
              <thead>
                <tr>
                  <th class="ps-3">{{ 'Item' | abpLocalization }}</th>
                  <th>{{ 'Description' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  <th class="pe-3"></th>
                </tr>
              </thead>
              <tbody formArrayName="materials">
                @for (mat of materials.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td class="ps-3"><input class="form-control form-control-sm" formControlName="itemId" /></td>
                    <td><input class="form-control form-control-sm" formControlName="description" /></td>
                    <td><input type="number" class="form-control form-control-sm text-end" formControlName="qty" min="0.01" step="0.01" /></td>
                    <td><input type="number" class="form-control form-control-sm text-end" formControlName="rate" min="0" step="0.01" /></td>
                    <td class="text-end font-monospace">{{ (mat.get('qty')?.value * mat.get('rate')?.value) | number:'1.2-2' }}</td>
                    <td class="pe-3"><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeMaterial(i)"><i class="fa fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="d-flex justify-content-between">
          <div class="fw-bold">{{ 'TotalCost' | abpLocalization }}: {{ totalCost | number:'1.2-2' }}</div>
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-outline-secondary" routerLink="/manufacturing/bom"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            <button type="submit" class="btn btn-primary"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </form>
    </abp-page>
  `,
})
export class BomFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);

  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    itemId: ['', Validators.required],
    itemName: [''],
    quantity: [1, [Validators.required, Validators.min(0.01)]],
    isActive: [true],
    materials: this.fb.array([]),
  });

  get materials(): FormArray { return this.form.get('materials') as FormArray; }

  get totalCost(): number {
    return this.materials.controls.reduce((sum, c) =>
      sum + (c.get('qty')?.value ?? 0) * (c.get('rate')?.value ?? 0), 0);
  }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;
    if (this.isEditMode) {
      this.http.get<any>(`/api/app/manufacturing/bom/${this.entityId}`).subscribe(bom => {
        this.form.patchValue({ itemId: bom.itemId, itemName: bom.itemName, quantity: bom.quantity, isActive: bom.isActive });
        (bom.items ?? []).forEach((item: any) => this.addMaterial(item));
      });
    }
  }

  addMaterial(item?: any): void {
    this.materials.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.itemName ?? ''],
      qty: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      rate: [item?.rate ?? 0, [Validators.required, Validators.min(0)]],
    }));
  }

  removeMaterial(index: number): void { this.materials.removeAt(index); }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const payload = {
      ...this.form.getRawValue(),
      items: this.materials.controls.map(c => c.getRawValue()),
    };
    const req = this.isEditMode
      ? this.http.put(`/api/app/manufacturing/bom/${this.entityId}`, payload)
      : this.http.post('/api/app/manufacturing/bom', payload);
    req.subscribe({
      next: () => { this.toaster.success('BOM saved'); this.router.navigate(['/manufacturing/bom']); },
      error: () => this.toaster.error('Save failed'),
    });
  }
}
