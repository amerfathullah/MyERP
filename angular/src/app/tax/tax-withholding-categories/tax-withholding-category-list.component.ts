import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { TaxWithholdingCategoryService } from '../../proxy/tax/tax-withholding-category.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface TaxWithholdingRateRow {
  id?: string;
  fromDate: string;
  toDate: string;
  rate: number;
  singleThreshold: number | null;
  cumulativeThreshold: number | null;
  group: string | null;
}

interface TaxWithholdingAccountRow {
  id?: string;
  companyId: string;
  accountId: string;
}

interface TaxWithholdingCategoryRow {
  id: string;
  categoryName: string;
  taxDeductionBasis: string;
  roundOffTaxAmount: boolean;
  taxOnExcessAmount: boolean;
  disableCumulativeThreshold: boolean;
  disableTransactionThreshold: boolean;
  rates: TaxWithholdingRateRow[];
  accounts: TaxWithholdingAccountRow[];
}

/**
 * Tax Withholding Category — rate table + posting accounts for withholding tax.
 * Per ERPNext: Tax Withholding Category (accounts/doctype/tax_withholding_category).
 * Referenced by TaxWithholdingEntry.TaxCategory for Malaysia Section 107A / WHT / PCB compliance.
 */
@Component({
  selector: 'app-tax-withholding-category-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-percentage me-2"></i>{{ '::TaxWithholdingCategories' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (categories().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-percentage fa-3x mb-2 d-block opacity-50"></i>
              <p>No tax withholding categories configured.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::CategoryName' | abpLocalization }}</th>
                  <th>{{ '::DeductTaxOnBasis' | abpLocalization }}</th>
                  <th>{{ '::Rates' | abpLocalization }}</th>
                  <th>{{ '::Accounts' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (c of categories(); track c.id) {
                  <tr>
                    <td class="fw-medium">{{ c.categoryName }}</td>
                    <td>{{ c.taxDeductionBasis }}</td>
                    <td>
                      @for (r of c.rates; track $index) {
                        <span class="badge bg-light text-dark me-1 mb-1">
                          {{ r.fromDate | date:'yyyy-MM-dd' }} → {{ r.toDate | date:'yyyy-MM-dd' }} @ {{ r.rate }}%
                        </span>
                      }
                    </td>
                    <td>{{ c.accounts.length }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editCategory(c)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteCategory(c.id)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditCategory' | abpLocalization) : ('::NewCategory' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-5">
                  <label class="form-label">{{ '::CategoryName' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="categoryName" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::DeductTaxOnBasis' | abpLocalization }}</label>
                  <select class="form-select" formControlName="taxDeductionBasis">
                    <option [ngValue]="0">Net Total</option>
                    <option [ngValue]="1">Gross Total</option>
                  </select>
                </div>
                <div class="col-md-4 d-flex align-items-end gap-3">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="roundOff" formControlName="roundOffTaxAmount" />
                    <label class="form-check-label" for="roundOff">{{ '::RoundOffTaxAmount' | abpLocalization }}</label>
                  </div>
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="onExcess" formControlName="taxOnExcessAmount" />
                    <label class="form-check-label" for="onExcess">{{ '::OnlyDeductOnExcess' | abpLocalization }}</label>
                  </div>
                </div>
              </div>
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="disableCumulative" formControlName="disableCumulativeThreshold" />
                    <label class="form-check-label" for="disableCumulative">{{ '::DisableCumulativeThreshold' | abpLocalization }}</label>
                  </div>
                </div>
                <div class="col-md-4">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="disableTransaction" formControlName="disableTransactionThreshold" />
                    <label class="form-check-label" for="disableTransaction">{{ '::DisableTransactionThreshold' | abpLocalization }}</label>
                  </div>
                </div>
              </div>

              <h6 class="mt-3">{{ '::TaxWithholdingRates' | abpLocalization }}</h6>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th style="width:40px">#</th>
                    <th>{{ '::FromDate' | abpLocalization }}</th>
                    <th>{{ '::ToDate' | abpLocalization }}</th>
                    <th>{{ '::Rate' | abpLocalization }} (%)</th>
                    <th>{{ '::TransactionThreshold' | abpLocalization }}</th>
                    <th>{{ '::CumulativeThreshold' | abpLocalization }}</th>
                    <th>{{ '::Group' | abpLocalization }}</th>
                    <th style="width:50px"></th>
                  </tr>
                </thead>
                <tbody formArrayName="rates">
                  @for (row of ratesArray.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td class="text-center text-muted">{{ i + 1 }}</td>
                      <td><input class="form-control form-control-sm" type="date" formControlName="fromDate" /></td>
                      <td><input class="form-control form-control-sm" type="date" formControlName="toDate" /></td>
                      <td><input class="form-control form-control-sm" type="number" formControlName="rate" /></td>
                      <td><input class="form-control form-control-sm" type="number" formControlName="singleThreshold" /></td>
                      <td><input class="form-control form-control-sm" type="number" formControlName="cumulativeThreshold" /></td>
                      <td><input class="form-control form-control-sm" formControlName="group" /></td>
                      <td>
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeRate(i)"><i class="fas fa-times"></i></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <button type="button" class="btn btn-outline-secondary btn-sm mb-3" (click)="addRate()">
                <i class="fas fa-plus me-1"></i>{{ '::AddRow' | abpLocalization }}
              </button>

              <h6 class="mt-3">{{ '::AccountDetails' | abpLocalization }}</h6>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th style="width:40px">#</th>
                    <th>{{ '::Company' | abpLocalization }}</th>
                    <th>{{ '::Account' | abpLocalization }}</th>
                    <th style="width:50px"></th>
                  </tr>
                </thead>
                <tbody formArrayName="accounts">
                  @for (row of accountsArray.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td class="text-center text-muted">{{ i + 1 }}</td>
                      <td><input class="form-control form-control-sm" formControlName="companyId" placeholder="Company Id" /></td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="accountId">
                          <option value="">—</option>
                          @for (acc of accounts(); track acc.id) {
                            <option [value]="acc.id">{{ acc.accountCode }} - {{ acc.name }}</option>
                          }
                        </select>
                      </td>
                      <td>
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeAccount(i)"><i class="fas fa-times"></i></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <button type="button" class="btn btn-outline-secondary btn-sm mb-3" (click)="addAccount()">
                <i class="fas fa-plus me-1"></i>{{ '::AddRow' | abpLocalization }}
              </button>

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
export class TaxWithholdingCategoryListComponent implements OnInit {
  private categoryService = inject(TaxWithholdingCategoryService);
  private accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  categories = signal<TaxWithholdingCategoryRow[]>([]);
  accounts = signal<any[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    categoryName: ['', Validators.required],
    taxDeductionBasis: [0],
    roundOffTaxAmount: [false],
    taxOnExcessAmount: [false],
    disableCumulativeThreshold: [false],
    disableTransactionThreshold: [false],
    rates: this.fb.array([]),
    accounts: this.fb.array([]),
  });

  get ratesArray(): FormArray { return this.form.get('rates') as FormArray; }
  get accountsArray(): FormArray { return this.form.get('accounts') as FormArray; }

  ngOnInit(): void {
    this.loadCategories();
    this.loadAccounts();
  }

  loadCategories(): void {
    this.loading.set(true);
    this.categoryService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.categories.set((res.items ?? []) as any); this.loading.set(false); },
      error: () => this.loading.set(false),
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
    this.form.reset({ taxDeductionBasis: 0, roundOffTaxAmount: false, taxOnExcessAmount: false, disableCumulativeThreshold: false, disableTransactionThreshold: false });
    this.ratesArray.clear();
    this.accountsArray.clear();
    this.addRate();
    this.addAccount();
    this.showForm.set(true);
  }

  editCategory(c: TaxWithholdingCategoryRow): void {
    this.editingId.set(c.id);
    this.form.patchValue({
      categoryName: c.categoryName,
      taxDeductionBasis: c.taxDeductionBasis === 'GrossTotal' ? 1 : 0,
      roundOffTaxAmount: c.roundOffTaxAmount,
      taxOnExcessAmount: c.taxOnExcessAmount,
      disableCumulativeThreshold: c.disableCumulativeThreshold,
      disableTransactionThreshold: c.disableTransactionThreshold,
    });
    this.ratesArray.clear();
    for (const r of c.rates) {
      this.ratesArray.push(this.fb.group({
        fromDate: [r.fromDate?.substring(0, 10)],
        toDate: [r.toDate?.substring(0, 10)],
        rate: [r.rate],
        singleThreshold: [r.singleThreshold],
        cumulativeThreshold: [r.cumulativeThreshold],
        group: [r.group],
      }));
    }
    this.accountsArray.clear();
    for (const a of c.accounts) {
      this.accountsArray.push(this.fb.group({
        companyId: [a.companyId, Validators.required],
        accountId: [a.accountId, Validators.required],
      }));
    }
    this.showForm.set(true);
  }

  addRate(): void {
    this.ratesArray.push(this.fb.group({
      fromDate: ['', Validators.required],
      toDate: ['', Validators.required],
      rate: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
      singleThreshold: [null],
      cumulativeThreshold: [null],
      group: [''],
    }));
  }

  removeRate(index: number): void {
    this.ratesArray.removeAt(index);
  }

  addAccount(): void {
    this.accountsArray.push(this.fb.group({
      companyId: ['', Validators.required],
      accountId: ['', Validators.required],
    }));
  }

  removeAccount(index: number): void {
    this.accountsArray.removeAt(index);
  }

  accountLabel(id: string): string {
    const acc = this.accounts().find(a => a.id === id);
    return acc ? `${acc.accountCode} - ${acc.name}` : id;
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      categoryName: this.form.value.categoryName!,
      taxDeductionBasis: this.form.value.taxDeductionBasis!,
      roundOffTaxAmount: !!this.form.value.roundOffTaxAmount,
      taxOnExcessAmount: !!this.form.value.taxOnExcessAmount,
      disableCumulativeThreshold: !!this.form.value.disableCumulativeThreshold,
      disableTransactionThreshold: !!this.form.value.disableTransactionThreshold,
      rates: this.form.value.rates as any[],
      accounts: this.form.value.accounts as any[],
    };

    const request$ = this.editingId()
      ? this.categoryService.update(this.editingId()!, payload as any)
      : this.categoryService.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadCategories();
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

  deleteCategory(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.categoryService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadCategories(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
