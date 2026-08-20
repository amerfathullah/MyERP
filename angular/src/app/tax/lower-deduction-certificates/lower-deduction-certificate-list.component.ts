import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { LowerDeductionCertificateService } from '../../proxy/tax/lower-deduction-certificate.service';
import { TaxWithholdingCategoryService } from '../../proxy/tax/tax-withholding-category.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import type { LowerDeductionCertificateDto } from '../../proxy/tax/models';

/**
 * Lower Deduction Certificate (LDC) — a supplier-held certificate entitling a reduced withholding
 * tax rate, up to a limit, for a Tax Withholding Category within a validity window. Consumed by
 * TaxWithholdingService.GetLdcDetailsAsync when Purchase Invoices calculate withholding tax.
 */
@Component({
  selector: 'app-lower-deduction-certificate-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-certificate me-2"></i>{{ '::LowerDeductionCertificates' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (certificates().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-certificate fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoLowerDeductionCertificatesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::CertificateNumber' | abpLocalization }}</th>
                  <th>{{ '::Supplier' | abpLocalization }}</th>
                  <th>{{ '::TaxWithholdingCategory' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::CertificateLimit' | abpLocalization }}</th>
                  <th>{{ '::ValidFrom' | abpLocalization }} - {{ '::ValidUpto' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (c of certificates(); track c.id) {
                  <tr>
                    <td class="fw-medium">{{ c.certificateNumber }}</td>
                    <td>{{ c.supplierName }}</td>
                    <td>{{ c.taxWithholdingCategoryName }}</td>
                    <td class="text-end">{{ c.rate }}%</td>
                    <td class="text-end">{{ c.certificateLimit | number:'1.2-2' }}</td>
                    <td>{{ c.validFrom | date:'dd/MM/yyyy' }} - {{ c.validUpto | date:'dd/MM/yyyy' }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editCertificate(c)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteCertificate(c.id!)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditLowerDeductionCertificate' | abpLocalization) : ('::NewLowerDeductionCertificate' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Supplier' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="supplierId">
                    <option value="">—</option>
                    @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::TaxWithholdingCategory' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="taxWithholdingCategoryId">
                    <option value="">—</option>
                    @for (cat of categories(); track cat.id) { <option [value]="cat.id">{{ cat.categoryName }}</option> }
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::CertificateNumber' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="certificateNumber" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Rate' | abpLocalization }} (%) *</label>
                  <input class="form-control" type="number" step="0.01" formControlName="rate" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::CertificateLimit' | abpLocalization }} *</label>
                  <input class="form-control" type="number" step="0.01" formControlName="certificateLimit" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::ValidFrom' | abpLocalization }} *</label>
                  <input class="form-control" type="date" formControlName="validFrom" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::ValidUpto' | abpLocalization }} *</label>
                  <input class="form-control" type="date" formControlName="validUpto" />
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
export class LowerDeductionCertificateListComponent implements OnInit {
  private ldcService = inject(LowerDeductionCertificateService);
  private categoryService = inject(TaxWithholdingCategoryService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  certificates = signal<LowerDeductionCertificateDto[]>([]);
  suppliers = signal<any[]>([]);
  categories = signal<any[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    supplierId: ['', Validators.required],
    taxWithholdingCategoryId: ['', Validators.required],
    certificateNumber: ['', Validators.required],
    rate: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    certificateLimit: [0, [Validators.required, Validators.min(0)]],
    validFrom: ['', Validators.required],
    validUpto: ['', Validators.required],
  });

  ngOnInit(): void {
    this.loadCertificates();
    this.loadSuppliers();
    this.loadCategories();
  }

  loadCertificates(): void {
    this.loading.set(true);
    const cid = this.companyContext.currentCompanyId();
    this.ldcService.getList({ companyId: cid || undefined, skipCount: 0, maxResultCount: 100, sorting: '' } as any).subscribe({
      next: res => { this.certificates.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  loadSuppliers(): void {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' } as any).subscribe({
      next: res => this.suppliers.set(res.items ?? []),
      error: () => {},
    });
  }

  loadCategories(): void {
    this.categoryService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => this.categories.set(res.items ?? []),
      error: () => {},
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ rate: 0, certificateLimit: 0 });
    this.showForm.set(true);
  }

  editCertificate(c: LowerDeductionCertificateDto): void {
    this.editingId.set(c.id!);
    this.form.patchValue({
      supplierId: c.supplierId,
      taxWithholdingCategoryId: c.taxWithholdingCategoryId,
      certificateNumber: c.certificateNumber,
      rate: c.rate,
      certificateLimit: c.certificateLimit,
      validFrom: c.validFrom?.substring(0, 10),
      validUpto: c.validUpto?.substring(0, 10),
    });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid) return;
    const cid = this.companyContext.currentCompanyId();
    if (!cid) {
      this.toaster.warn('::SelectCompanyFirst');
      return;
    }
    this.saving.set(true);

    const payload = {
      companyId: cid,
      supplierId: this.form.value.supplierId!,
      taxWithholdingCategoryId: this.form.value.taxWithholdingCategoryId!,
      certificateNumber: this.form.value.certificateNumber!,
      rate: this.form.value.rate!,
      certificateLimit: this.form.value.certificateLimit!,
      validFrom: this.form.value.validFrom!,
      validUpto: this.form.value.validUpto!,
    };

    const request$ = this.editingId()
      ? this.ldcService.update(this.editingId()!, payload)
      : this.ldcService.create(payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadCertificates();
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

  deleteCertificate(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.ldcService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadCertificates(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
