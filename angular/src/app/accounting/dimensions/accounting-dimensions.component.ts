import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AccountingDimensionService } from '../../proxy/accounting/accounting-dimension.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AccountingDimensionDto, AccountingDimensionFilterDto, AccountDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-accounting-dimensions',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::AccountingDimensions' | abpLocalization">
      <!-- Create form -->
      <div class="card mb-4">
        <div class="card-header">
          <h6 class="card-title mb-0"><i class="fa fa-plus me-2"></i>{{ '::NewDimension' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="create()" class="row g-3 align-items-end">
            <div class="col-md-3">
              <label class="form-label">{{ '::DocumentType' | abpLocalization }}</label>
              <select class="form-select" formControlName="documentType">
                <option value="" disabled>Select...</option>
                <option value="Branch">Branch</option>
                <option value="Department">Department</option>
                <option value="CostCenter">Cost Center</option>
                <option value="Project">Project</option>
                <option value="Territory">Territory</option>
                <option value="CustomerGroup">Customer Group</option>
                <option value="SupplierGroup">Supplier Group</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ '::Label' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="label" [placeholder]="'::Placeholder:DimensionExample' | abpLocalization" />
            </div>
            <div class="col-md-2">
              <div class="form-check mt-4">
                <input class="form-check-input" type="checkbox" formControlName="isMandatory" id="isMandatory">
                <label class="form-check-label" for="isMandatory">{{ '::Mandatory' | abpLocalization }}</label>
              </div>
            </div>
            <div class="col-md-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving()">
                <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
              </button>
            </div>
          </form>
        </div>
      </div>

      <!-- Enabled Dimensions Preview -->
      @if (enabledDimensions().length > 0) {
        <div class="alert alert-light border d-flex flex-wrap align-items-center gap-2 mb-3">
          <strong class="small text-muted me-1">
            <i class="fa fa-check-circle text-success me-1"></i>{{ '::EnabledDimensions' | abpLocalization }} ({{ '::ActiveForCompany' | abpLocalization }}):
          </strong>
          @for (ed of enabledDimensions(); track ed.id) {
            <span class="badge bg-primary">
              {{ ed.label || ed.documentType }}
              @if (ed.isMandatory) { <span class="ms-1 text-warning">*</span> }
            </span>
          }
        </div>
      }

      <!-- Dimensions list -->
      <div class="card">
        <div class="card-header">
          <h6 class="card-title mb-0">{{ '::AccountingDimensions' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          @if (loading()) {
            <div class="text-center p-4"><i class="fa fa-spinner fa-spin"></i></div>
          } @else if (dimensions().length === 0) {
            <div class="text-center text-muted p-4">
              <i class="fa fa-cubes fa-2x mb-2 d-block"></i>
              {{ '::NoDimensionsYet' | abpLocalization }}
            </div>
          } @else {
            <table class="table table-hover">
              <thead>
                <tr>
                  <th>{{ '::DocumentType' | abpLocalization }}</th>
                  <th>{{ '::Label' | abpLocalization }}</th>
                  <th>{{ '::FieldName' | abpLocalization }}</th>
                  <th>{{ '::Mandatory' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (dim of dimensions(); track dim.id) {
                  <tr>
                    <td>{{ dim.documentType }}</td>
                    <td>{{ dim.label }}</td>
                    <td><code>{{ dim.fieldName }}</code></td>
                    <td>
                      @if (dim.isMandatory) {
                        <span class="badge bg-warning">{{ '::Mandatory' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-light text-dark">Optional</span>
                      }
                    </td>
                    <td>
                      @if (dim.isEnabled) {
                        <span class="badge bg-success">Enabled</span>
                      } @else {
                        <span class="badge bg-secondary">Disabled</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button type="button" class="btn btn-outline-primary" [title]="'::DimensionFilters' | abpLocalization" (click)="selectDimensionForFilters(dim)">
                          <i class="fa fa-filter"></i>
                        </button>
                        @if (dim.isEnabled) {
                          <button class="btn btn-outline-secondary" (click)="toggleEnable(dim, false)">
                            <i class="fa fa-ban"></i>
                          </button>
                        } @else {
                          <button class="btn btn-outline-success" (click)="toggleEnable(dim, true)">
                            <i class="fa fa-check"></i>
                          </button>
                        }
                        <button class="btn btn-outline-danger" (click)="remove(dim)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>

      <!-- Dimension Filters -->
      @if (selectedDimension(); as activeDim) {
        <div class="card mt-4 border-primary">
          <div class="card-header bg-primary bg-opacity-10 d-flex justify-content-between align-items-center">
            <h6 class="card-title mb-0">
              <i class="fa fa-filter me-2"></i>{{ '::DimensionFilters' | abpLocalization }} — {{ activeDim.label }} ({{ activeDim.documentType }})
            </h6>
            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="selectedDimension.set(null)">
              <i class="fa fa-times"></i>
            </button>
          </div>
          <div class="card-body">
            <form [formGroup]="filterForm" (ngSubmit)="createFilter()" class="row g-3 align-items-end mb-4">
              <div class="col-md-4">
                <label class="form-label">{{ '::Account' | abpLocalization }}</label>
                <select class="form-select" formControlName="accountId">
                  <option value="" disabled>{{ '::Select' | abpLocalization }}...</option>
                  @for (acc of accounts(); track acc.id) {
                    <option [value]="acc.id">{{ acc.accountCode }} — {{ acc.accountName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::FilterType' | abpLocalization }}</label>
                <select class="form-select" formControlName="isAllowList">
                  <option [ngValue]="true">{{ '::AllowList' | abpLocalization }}</option>
                  <option [ngValue]="false">{{ '::RestrictList' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::DimensionValues' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="dimensionValueIds" [placeholder]="'::Placeholder:DimensionValueIdsSample' | abpLocalization" />
              </div>
              <div class="col-md-2">
                <button type="submit" class="btn btn-primary w-100" [disabled]="filterForm.invalid || savingFilter()">
                  <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
                </button>
              </div>
            </form>

            @if (loadingFilters()) {
              <div class="text-center p-3"><i class="fa fa-spinner fa-spin"></i></div>
            } @else if (filters().length === 0) {
              <p class="text-muted text-center mb-0">{{ '::NoDimensionFiltersYet' | abpLocalization }}</p>
            } @else {
              <table class="table table-sm table-hover mb-0">
                <thead>
                  <tr>
                    <th>{{ '::Account' | abpLocalization }}</th>
                    <th>{{ '::FilterType' | abpLocalization }}</th>
                    <th>{{ '::DimensionValues' | abpLocalization }}</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (flt of filters(); track flt.id) {
                    <tr>
                      <td>{{ getAccountLabel(flt.accountId) }}</td>
                      <td>
                        @if (flt.isAllowList) {
                          <span class="badge bg-success">{{ '::AllowList' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-danger">{{ '::RestrictList' | abpLocalization }}</span>
                        }
                      </td>
                      <td><code>{{ flt.dimensionValueIds || '—' }}</code></td>
                      <td class="text-end">
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="deleteFilter(flt.id!)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>
      }
    </abp-page>
  `
})
export class AccountingDimensionsComponent implements OnInit {
  private dimensionService = inject(AccountingDimensionService);
  private accountService = inject(AccountService);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  dimensions = signal<AccountingDimensionDto[]>([]);
  enabledDimensions = signal<AccountingDimensionDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  selectedDimension = signal<AccountingDimensionDto | null>(null);
  filters = signal<AccountingDimensionFilterDto[]>([]);
  loading = signal(true);
  loadingFilters = signal(false);
  saving = signal(false);
  savingFilter = signal(false);

  form = this.fb.group({
    documentType: ['', Validators.required],
    label: ['', Validators.required],
    isMandatory: [false]
  });

  filterForm = this.fb.group({
    accountId: ['', Validators.required],
    isAllowList: [true, Validators.required],
    dimensionValueIds: ['', Validators.required],
  });

  ngOnInit() {
    this.loadDimensions();
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) {
      this.accountService.getList({ companyId, skipCount: 0, maxResultCount: 500 } as any).subscribe({
        next: res => this.accounts.set(res.items ?? []),
        error: () => {},
      });
    }
  }

  loadDimensions() {
    this.loading.set(true);
    this.dimensionService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: (res) => {
        this.dimensions.set(res.items ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
    const companyId = this.companyContext.currentCompanyId() ?? undefined;
    this.dimensionService.getEnabledDimensions?.(companyId)?.subscribe({
      next: (list) => this.enabledDimensions.set(list ?? []),
      error: () => {},
    });
  }

  create() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.dimensionService.create(this.form.value as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.form.reset({ documentType: '', label: '', isMandatory: false });
        this.saving.set(false);
        this.loadDimensions();
      },
      error: () => this.saving.set(false)
    });
  }

  toggleEnable(dim: AccountingDimensionDto, enable: boolean) {
    const action = enable ? this.dimensionService.enable(dim.id!) : this.dimensionService.disable(dim.id!);
    action.subscribe({
      next: () => this.loadDimensions(),
      error: () => {}
    });
  }

  remove(dim: AccountingDimensionDto) {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.dimensionService.delete(dim.id!).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          if (this.selectedDimension()?.id === dim.id) {
            this.selectedDimension.set(null);
          }
          this.loadDimensions();
        }
      });
    });
  }

  selectDimensionForFilters(dim: AccountingDimensionDto): void {
    this.selectedDimension.set(dim);
    this.loadFilters(dim.id!);
  }

  loadFilters(dimensionId: string): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) {
      this.filters.set([]);
      return;
    }
    this.loadingFilters.set(true);
    this.dimensionService.getFilters(dimensionId, companyId).subscribe({
      next: list => {
        this.filters.set(list ?? []);
        this.loadingFilters.set(false);
      },
      error: () => this.loadingFilters.set(false),
    });
  }

  createFilter(): void {
    const activeDim = this.selectedDimension();
    const companyId = this.companyContext.currentCompanyId();
    if (!activeDim?.id || !companyId || this.filterForm.invalid || this.savingFilter()) return;

    this.savingFilter.set(true);
    const val = this.filterForm.getRawValue();
    this.dimensionService.createFilter({
      accountingDimensionId: activeDim.id,
      companyId,
      accountId: val.accountId!,
      isAllowList: val.isAllowList ?? true,
      dimensionValueIds: val.dimensionValueIds ?? '',
    }).subscribe({
      next: () => {
        this.savingFilter.set(false);
        this.filterForm.reset({ accountId: '', isAllowList: true, dimensionValueIds: '' });
        this.toaster.success(this.l.instant('::SuccessfullyCreated'));
        this.loadFilters(activeDim.id!);
      },
      error: (err: any) => {
        this.savingFilter.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }

  deleteFilter(filterId: string): void {
    const activeDim = this.selectedDimension();
    if (!filterId || !activeDim?.id) return;
    this.dimensionService.deleteFilter(filterId).subscribe({
      next: () => {
        this.toaster.success(this.l.instant('::SuccessfullyDeleted'));
        this.loadFilters(activeDim.id!);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed')),
    });
  }

  getAccountLabel(accountId?: string): string {
    if (!accountId) return '—';
    const acc = this.accounts().find(a => a.id === accountId);
    return acc ? `${acc.accountCode ?? ''} ${acc.accountName ?? ''}`.trim() : accountId;
  }
}
