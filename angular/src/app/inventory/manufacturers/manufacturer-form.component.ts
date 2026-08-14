import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturerService } from '../../proxy/inventory/manufacturer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-manufacturer-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">
          <i class="bi bi-building-gear me-2"></i>
          {{ (isEditMode ? 'MyERP::EditManufacturer' : 'MyERP::NewManufacturer') | abpLocalization }}
        </h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::ShortName' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="shortName" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::FullName' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="fullName" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Country' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="country" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Website' | abpLocalization }}</label>
              <input type="url" class="form-control" formControlName="website" placeholder="https://" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::LogoUrl' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="logoUrl" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-12">
              <label class="form-label">{{ 'MyERP::Notes' | abpLocalization }}</label>
              <textarea class="form-control" rows="3" formControlName="notes"></textarea>
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
export class ManufacturerFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ManufacturerService);
  private readonly companyContext = inject(CompanyContextService);
  private readonly toaster = inject(ToasterService);

  id?: string;
  isEditMode = false;
  isSaving = false;

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    shortName: ['', Validators.required],
    fullName: [''],
    country: [''],
    website: [''],
    logoUrl: [''],
    notes: [''],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.params['id'];
    this.isEditMode = !!this.id;

    const currentCompanyId = this.companyContext.selectedCompanyId();
    if (currentCompanyId) {
      this.form.patchValue({ companyId: currentCompanyId });
    }

    if (this.isEditMode && this.id) {
      this.service.get(this.id).subscribe((mfr) => {
        this.form.patchValue({
          companyId: mfr.companyId,
          shortName: mfr.shortName,
          fullName: mfr.fullName,
          country: mfr.country,
          website: mfr.website,
          logoUrl: mfr.logoUrl,
          notes: mfr.notes,
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
