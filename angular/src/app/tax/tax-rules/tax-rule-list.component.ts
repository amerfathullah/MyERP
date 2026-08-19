import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TaxRuleService } from '../../proxy/tax/tax-rule.service';
import { TaxCategoryService } from '../../proxy/tax/tax-category.service';
import { TaxCategoryDto, TaxRuleDto } from '../../proxy/tax/models';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

/**
 * Tax Rule — rate + effective date range + item group/region filters for a Tax Category.
 * Per ERPNext: Tax Rule (accounts/doctype/tax_rule). Rules are scoped per Tax Category,
 * evaluated by priority to resolve the applicable rate for a transaction.
 */
@Component({
  selector: 'app-tax-rule-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-gavel me-2"></i>{{ '::TaxRules' | abpLocalization }}</h5>
          <div class="d-flex gap-2 align-items-center">
            <select class="form-select form-select-sm" style="width:220px" [ngModel]="selectedCategoryId()" (ngModelChange)="onCategoryChange($event)">
              <option [ngValue]="null">{{ '::SelectTaxCategory' | abpLocalization }}</option>
              @for (c of categories(); track c.id) {
                <option [ngValue]="c.id">{{ c.code }} - {{ c.name }}</option>
              }
            </select>
            <button class="btn btn-primary btn-sm" [disabled]="!selectedCategoryId()" (click)="openForm()">
              <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
            </button>
          </div>
        </div>

        <div class="card-body p-0">
          @if (!selectedCategoryId()) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-gavel fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::SelectTaxCategoryToViewRules' | abpLocalization }}</p>
            </div>
          } @else if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (rules().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-gavel fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoTaxRulesConfigured' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Rate' | abpLocalization }}</th>
                  <th>{{ '::EffectiveFrom' | abpLocalization }}</th>
                  <th>{{ '::EffectiveTo' | abpLocalization }}</th>
                  <th>{{ '::ItemGroup' | abpLocalization }}</th>
                  <th>{{ '::Region' | abpLocalization }}</th>
                  <th>{{ '::Priority' | abpLocalization }}</th>
                  <th>{{ '::Active' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (r of rules(); track r.id) {
                  <tr>
                    <td class="fw-medium">{{ r.rate }}%</td>
                    <td>{{ r.effectiveFrom | date:'yyyy-MM-dd' }}</td>
                    <td>{{ r.effectiveTo ? (r.effectiveTo | date:'yyyy-MM-dd') : '—' }}</td>
                    <td>{{ r.itemGroupFilter || '—' }}</td>
                    <td>{{ r.regionFilter || '—' }}</td>
                    <td>{{ r.priority }}</td>
                    <td>
                      @if (r.isActive) {
                        <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ '::Inactive' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editRule(r)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteRule(r.id!)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditTaxRule' | abpLocalization) : ('::NewTaxRule' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-3">
                  <label class="form-label">{{ '::Rate' | abpLocalization }} (%) *</label>
                  <input class="form-control" type="number" formControlName="rate" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::EffectiveFrom' | abpLocalization }} *</label>
                  <input class="form-control" type="date" formControlName="effectiveFrom" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::EffectiveTo' | abpLocalization }}</label>
                  <input class="form-control" type="date" formControlName="effectiveTo" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Priority' | abpLocalization }}</label>
                  <input class="form-control" type="number" formControlName="priority" />
                </div>
              </div>
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::ItemGroup' | abpLocalization }}</label>
                  <input class="form-control" formControlName="itemGroupFilter" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::Region' | abpLocalization }}</label>
                  <input class="form-control" formControlName="regionFilter" />
                </div>
                <div class="col-md-4 d-flex align-items-end">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isActive" formControlName="isActive" />
                    <label class="form-check-label" for="isActive">{{ '::Active' | abpLocalization }}</label>
                  </div>
                </div>
              </div>
              <div class="row g-3 mb-3">
                <div class="col-md-12">
                  <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                  <textarea class="form-control" rows="2" formControlName="description"></textarea>
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
export class TaxRuleListComponent implements OnInit {
  private ruleService = inject(TaxRuleService);
  private categoryService = inject(TaxCategoryService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  categories = signal<TaxCategoryDto[]>([]);
  rules = signal<TaxRuleDto[]>([]);
  selectedCategoryId = signal<string | null>(null);
  loading = signal(false);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    rate: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    effectiveFrom: ['', Validators.required],
    effectiveTo: [''],
    itemGroupFilter: [''],
    regionFilter: [''],
    priority: [0],
    description: [''],
    isActive: [true],
  });

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {
    this.categoryService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'code asc' }).subscribe({
      next: res => this.categories.set(res.items ?? []),
      error: () => {},
    });
  }

  onCategoryChange(categoryId: string | null): void {
    this.selectedCategoryId.set(categoryId);
    this.showForm.set(false);
    if (categoryId) {
      this.loadRules(categoryId);
    } else {
      this.rules.set([]);
    }
  }

  loadRules(categoryId: string): void {
    this.loading.set(true);
    this.ruleService.getList(categoryId, { skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.rules.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ rate: 0, priority: 0, isActive: true });
    this.showForm.set(true);
  }

  editRule(r: TaxRuleDto): void {
    this.editingId.set(r.id!);
    this.form.patchValue({
      rate: r.rate,
      effectiveFrom: r.effectiveFrom?.substring(0, 10),
      effectiveTo: r.effectiveTo ? r.effectiveTo.substring(0, 10) : '',
      itemGroupFilter: r.itemGroupFilter,
      regionFilter: r.regionFilter,
      priority: r.priority,
      description: r.description,
      isActive: r.isActive,
    });
    this.showForm.set(true);
  }

  save(): void {
    if (this.form.invalid || !this.selectedCategoryId()) return;
    this.saving.set(true);

    const payload = {
      taxCategoryId: this.selectedCategoryId()!,
      rate: this.form.value.rate!,
      effectiveFrom: this.form.value.effectiveFrom!,
      effectiveTo: this.form.value.effectiveTo || null,
      itemGroupFilter: this.form.value.itemGroupFilter || null,
      regionFilter: this.form.value.regionFilter || null,
      priority: this.form.value.priority ?? 0,
      description: this.form.value.description || null,
      isActive: !!this.form.value.isActive,
    };

    const request$ = this.editingId()
      ? this.ruleService.update(this.editingId()!, payload)
      : this.ruleService.create(payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadRules(this.selectedCategoryId()!);
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

  deleteRule(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.ruleService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadRules(this.selectedCategoryId()!); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
