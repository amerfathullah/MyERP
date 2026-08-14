import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomsTariffNumberService } from '../../proxy/inventory/customs-tariff-number.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-customs-tariff-number-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-tag me-2"></i>
          {{ (isEditMode ? 'MyERP::EditCustomsTariffNumber' : 'MyERP::NewCustomsTariffNumber') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::TariffNumber' | abpLocalization }} *</label>
              <input type="text" class="form-control font-monospace" formControlName="tariffNumber"
                placeholder="e.g. 8471.30.0000" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Description' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="description" />
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <a routerLink=".." class="btn btn-secondary btn-sm">
              {{ 'MyERP::Cancel' | abpLocalization }}
            </a>
            <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || isSaving">
              @if (isSaving) {
                <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              }
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CustomsTariffNumberFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CustomsTariffNumberService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly toaster = inject(ToasterService);

  id?: string;
  isEditMode = false;
  isSaving = false;

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    tariffNumber: ['', Validators.required],
    description: [''],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.params['id'];
    this.isEditMode = !!this.id;

    const currentCompanyId = this.companyContext.selectedCompanyId();
    if (currentCompanyId) {
      this.form.patchValue({ companyId: currentCompanyId });
    }

    if (this.isEditMode && this.id) {
      this.service.get(this.id).subscribe((item) => {
        this.form.patchValue({
          companyId: item.companyId,
          tariffNumber: item.tariffNumber,
          description: item.description,
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving = true;
    const value = this.form.value;

    const request$ = this.isEditMode && this.id
      ? this.service.update(this.id, value)
      : this.service.create(value);

    request$.subscribe({
      next: () => {
        this.isSaving = false;
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['..'], { relativeTo: this.route });
      },
      error: (err) => {
        this.isSaving = false;
        this.toaster.error(err?.error?.error?.message ?? 'Save failed');
      }
    });
  }
}
