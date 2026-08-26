import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { SupplierScorecardVariableService } from '../../proxy/purchasing/supplier-scorecard-variable.service';

@Component({
  selector: 'app-scorecard-variable-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Supplier Scorecard Variable</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Variable Label *</label>
              <input type="text" class="form-control" formControlName="variableLabel" placeholder="e.g. Total Ordered Quantity">
            </div>
            <div class="col-md-6">
              <label class="form-label">Parameter Name *</label>
              <input type="text" class="form-control" formControlName="paramName" placeholder="e.g. total_ordered_qty">
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-8">
              <label class="form-label">Path *</label>
              <input type="text" class="form-control" formControlName="path" placeholder="e.g. doc.total_ordered_qty">
            </div>
            <div class="col-md-4 d-flex align-items-end">
              <div class="form-check">
                <input type="checkbox" class="form-check-input" id="isCustom" formControlName="isCustom">
                <label class="form-check-label" for="isCustom">Custom Variable</label>
              </div>
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">Description</label>
            <textarea class="form-control" rows="3" formControlName="description" placeholder="Optional description..."></textarea>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/purchasing/scorecard-variables" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ScorecardVariableFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(SupplierScorecardVariableService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;

  constructor() {
    this.form = this.fb.group({
      variableLabel: ['', [Validators.required, Validators.maxLength(140)]],
      paramName: ['', [Validators.required, Validators.maxLength(140)]],
      path: ['', [Validators.required, Validators.maxLength(255)]],
      isCustom: [false],
      description: ['', Validators.maxLength(500)],
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
        this.router.navigate(['/purchasing/scorecard-variables']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
