import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { DunningTypeService } from '../../proxy/sales/dunning-type.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface DunningTypeRow {
  id: string;
  companyId: string;
  dunningTypeName: string;
  isDefault: boolean;
  dunningFee: number;
  rateOfInterest: number;
  incomeAccountId: string | null;
  costCenterId: string | null;
  letterText: { language: string | null; isDefaultLanguage: boolean; bodyText: string | null; closingText: string | null }[];
}

/**
 * Dunning Type management — per-company collections-level config: default fee, yearly
 * interest rate, posting accounts, language-templated letter text.
 * Per ERPNext: Dunning Type (accounts/doctype/dunning_type). Referenced by Dunning
 * (fetch_from: dunning_fee, rate_of_interest, income_account, cost_center).
 */
@Component({
  selector: 'app-dunning-type-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-file-invoice-dollar me-2"></i>{{ '::DunningTypes' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (types().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-file-invoice-dollar fa-3x mb-2 d-block opacity-50"></i>
              <p>No dunning types configured.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::DunningTypeName' | abpLocalization }}</th>
                  <th class="text-end">{{ '::DunningFee' | abpLocalization }}</th>
                  <th class="text-end">{{ '::RateOfInterest' | abpLocalization }} (%)</th>
                  <th class="text-center">{{ '::Default' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (t of types(); track t.id) {
                  <tr>
                    <td class="fw-medium">{{ t.dunningTypeName }}</td>
                    <td class="text-end">{{ t.dunningFee | number:'1.2-2' }}</td>
                    <td class="text-end">{{ t.rateOfInterest }}</td>
                    <td class="text-center">
                      @if (t.isDefault) { <span class="badge bg-success">{{ '::Default' | abpLocalization }}</span> }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editType(t)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteType(t.id)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditDunningType' | abpLocalization) : ('::NewDunningType' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::DunningTypeName' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="dunningTypeName" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::DunningFee' | abpLocalization }}</label>
                  <input class="form-control" type="number" formControlName="dunningFee" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::RateOfInterest' | abpLocalization }} (%)</label>
                  <input class="form-control" type="number" formControlName="rateOfInterest" />
                </div>
                <div class="col-md-4 d-flex align-items-end">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isDefault" formControlName="isDefault" />
                    <label class="form-check-label" for="isDefault">{{ '::IsDefaultForCompany' | abpLocalization }}</label>
                  </div>
                </div>
              </div>

              <div class="row g-3 mb-3">
                <div class="col-md-6">
                  <label class="form-label">{{ '::IncomeAccount' | abpLocalization }}</label>
                  <select class="form-select" formControlName="incomeAccountId">
                    <option value="">—</option>
                    @for (acc of accounts(); track acc.id) {
                      <option [value]="acc.id">{{ acc.accountCode }} - {{ acc.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-6">
                  <label class="form-label">{{ '::CostCenter' | abpLocalization }}</label>
                  <select class="form-select" formControlName="costCenterId">
                    <option value="">—</option>
                    @for (cc of costCenters(); track cc.id) {
                      <option [value]="cc.id">{{ cc.name }}</option>
                    }
                  </select>
                </div>
              </div>

              <h6 class="mt-3">{{ '::DunningLetterText' | abpLocalization }}</h6>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th style="width:40px">#</th>
                    <th>{{ '::Language' | abpLocalization }}</th>
                    <th class="text-center">{{ '::Default' | abpLocalization }}</th>
                    <th>{{ '::BodyText' | abpLocalization }}</th>
                    <th>{{ '::ClosingText' | abpLocalization }}</th>
                    <th style="width:50px"></th>
                  </tr>
                </thead>
                <tbody formArrayName="letterText">
                  @for (row of letterTextArray.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td class="text-center text-muted">{{ i + 1 }}</td>
                      <td><input class="form-control form-control-sm" formControlName="language" placeholder="en" /></td>
                      <td class="text-center"><input class="form-check-input" type="checkbox" formControlName="isDefaultLanguage" /></td>
                      <td><textarea class="form-control form-control-sm" rows="2" formControlName="bodyText"></textarea></td>
                      <td><textarea class="form-control form-control-sm" rows="2" formControlName="closingText"></textarea></td>
                      <td>
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeLetterText(i)"><i class="fas fa-times"></i></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <button type="button" class="btn btn-outline-secondary btn-sm mb-3" (click)="addLetterText()">
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
export class DunningTypeListComponent implements OnInit {
  private typeService = inject(DunningTypeService);
  private accountService = inject(AccountService);
  private costCenterService = inject(CostCenterService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  types = signal<DunningTypeRow[]>([]);
  accounts = signal<any[]>([]);
  costCenters = signal<any[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    dunningTypeName: ['', Validators.required],
    dunningFee: [0],
    rateOfInterest: [0],
    isDefault: [false],
    incomeAccountId: [''],
    costCenterId: [''],
    letterText: this.fb.array([]),
  });

  get letterTextArray(): FormArray { return this.form.get('letterText') as FormArray; }

  ngOnInit(): void {
    this.loadTypes();
    this.loadAccounts();
    this.loadCostCenters();
  }

  loadTypes(): void {
    this.loading.set(true);
    this.typeService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.types.set((res.items ?? []) as any); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  loadAccounts(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' } as any).subscribe({
      next: res => this.accounts.set(res.items ?? []),
      error: () => {},
    });
  }

  loadCostCenters(): void {
    this.costCenterService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' } as any).subscribe({
      next: res => this.costCenters.set(res.items ?? []),
      error: () => {},
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ dunningFee: 0, rateOfInterest: 0, isDefault: false });
    this.letterTextArray.clear();
    this.showForm.set(true);
  }

  editType(t: DunningTypeRow): void {
    this.editingId.set(t.id);
    this.form.patchValue({
      dunningTypeName: t.dunningTypeName,
      dunningFee: t.dunningFee,
      rateOfInterest: t.rateOfInterest,
      isDefault: t.isDefault,
      incomeAccountId: t.incomeAccountId ?? '',
      costCenterId: t.costCenterId ?? '',
    });
    this.letterTextArray.clear();
    for (const row of t.letterText ?? []) {
      this.letterTextArray.push(this.fb.group({
        language: [row.language],
        isDefaultLanguage: [row.isDefaultLanguage],
        bodyText: [row.bodyText],
        closingText: [row.closingText],
      }));
    }
    this.showForm.set(true);
  }

  addLetterText(): void {
    this.letterTextArray.push(this.fb.group({
      language: [''],
      isDefaultLanguage: [false],
      bodyText: [''],
      closingText: [''],
    }));
  }

  removeLetterText(index: number): void {
    this.letterTextArray.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      companyId: this.companyContext.currentCompanyId(),
      dunningTypeName: this.form.value.dunningTypeName!,
      dunningFee: this.form.value.dunningFee ?? 0,
      rateOfInterest: this.form.value.rateOfInterest ?? 0,
      isDefault: !!this.form.value.isDefault,
      incomeAccountId: this.form.value.incomeAccountId || null,
      costCenterId: this.form.value.costCenterId || null,
      letterText: this.form.value.letterText as any[],
    };

    const request$ = this.editingId()
      ? this.typeService.update(this.editingId()!, payload as any)
      : this.typeService.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadTypes();
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

  deleteType(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.typeService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadTypes(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
