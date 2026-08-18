import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { BrandService } from '../../proxy/inventory/brand.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface BrandRow {
  id: string;
  name: string;
  description?: string | null;
  defaultWarehouseId?: string | null;
  defaultIncomeAccountId?: string | null;
  defaultExpenseAccountId?: string | null;
  isActive?: boolean;
}

/**
 * Item Brand master. Per ERPNext: Brand (setup/doctype/brand).
 * Referenced by Item.Brand and PricingRule/PromotionalScheme brand filters by name.
 */
@Component({
  selector: 'app-brand-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-certificate me-2"></i>{{ '::Brands' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (brands().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-certificate fa-3x mb-2 d-block opacity-50"></i>
              <p>No brands configured.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::Description' | abpLocalization }}</th>
                  <th>{{ '::Active' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (b of brands(); track b.id) {
                  <tr>
                    <td class="fw-medium">{{ b.name }}</td>
                    <td>{{ b.description }}</td>
                    <td>
                      @if (b.isActive) {
                        <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ '::Inactive' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editBrand(b)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteBrand(b.id)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditBrand' | abpLocalization) : ('::NewBrand' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="name" />
                </div>
                <div class="col-md-8">
                  <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                  <input class="form-control" formControlName="description" />
                </div>
              </div>
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::DefaultWarehouse' | abpLocalization }}</label>
                  <select class="form-select" formControlName="defaultWarehouseId">
                    <option [ngValue]="null">—</option>
                    @for (w of warehouses(); track w.id) {
                      <option [ngValue]="w.id">{{ w.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::DefaultIncomeAccount' | abpLocalization }}</label>
                  <select class="form-select" formControlName="defaultIncomeAccountId">
                    <option [ngValue]="null">—</option>
                    @for (acc of accounts(); track acc.id) {
                      <option [ngValue]="acc.id">{{ acc.accountCode }} - {{ acc.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::DefaultExpenseAccount' | abpLocalization }}</label>
                  <select class="form-select" formControlName="defaultExpenseAccountId">
                    <option [ngValue]="null">—</option>
                    @for (acc of accounts(); track acc.id) {
                      <option [ngValue]="acc.id">{{ acc.accountCode }} - {{ acc.name }}</option>
                    }
                  </select>
                </div>
              </div>
              <div class="form-check mb-3">
                <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive" />
                <label class="form-check-label" for="isActive">{{ '::Active' | abpLocalization }}</label>
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
export class BrandListComponent implements OnInit {
  private brandService = inject(BrandService);
  private warehouseService = inject(WarehouseService);
  private accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  brands = signal<BrandRow[]>([]);
  warehouses = signal<any[]>([]);
  accounts = signal<any[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    name: ['', Validators.required],
    description: [''],
    defaultWarehouseId: [null as string | null],
    defaultIncomeAccountId: [null as string | null],
    defaultExpenseAccountId: [null as string | null],
    isActive: [true],
  });

  ngOnInit(): void {
    this.loadBrands();
    this.loadWarehouses();
    this.loadAccounts();
  }

  loadBrands(): void {
    this.loading.set(true);
    this.brandService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.brands.set((res.items ?? []) as any); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  loadWarehouses(): void {
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: res => this.warehouses.set(res.items ?? []),
      error: () => {},
    });
  }

  loadAccounts(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' } as any).subscribe({
      next: res => this.accounts.set(res.items ?? []),
      error: () => {},
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', description: '', defaultWarehouseId: null, defaultIncomeAccountId: null, defaultExpenseAccountId: null, isActive: true });
    this.showForm.set(true);
  }

  editBrand(b: BrandRow): void {
    this.editingId.set(b.id);
    this.form.patchValue({
      name: b.name,
      description: b.description,
      defaultWarehouseId: b.defaultWarehouseId,
      defaultIncomeAccountId: b.defaultIncomeAccountId,
      defaultExpenseAccountId: b.defaultExpenseAccountId,
      isActive: b.isActive,
    });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      name: this.form.value.name!,
      description: this.form.value.description || null,
      defaultWarehouseId: this.form.value.defaultWarehouseId || null,
      defaultIncomeAccountId: this.form.value.defaultIncomeAccountId || null,
      defaultExpenseAccountId: this.form.value.defaultExpenseAccountId || null,
      isActive: !!this.form.value.isActive,
    };

    const request$ = this.editingId()
      ? this.brandService.update(this.editingId()!, payload as any)
      : this.brandService.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadBrands();
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

  deleteBrand(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.brandService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadBrands(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
