import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ItemTaxTemplateService } from '../../proxy/tax/item-tax-template.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface ItemTaxTemplateRow {
  id: string;
  companyId: string;
  title: string;
  isDisabled: boolean;
  details: ItemTaxTemplateDetailRow[];
}

interface ItemTaxTemplateDetailRow {
  id?: string;
  taxAccountId: string;
  taxRate: number;
  notApplicable: boolean;
}

/**
 * Item Tax Template Management — per-item tax rate overrides.
 * Per ERPNext: Item Tax Template (accounts/doctype/item_tax_template). Assigned on Item master
 * to override the document-level tax rate for matching tax accounts. NotApplicable rows exclude
 * that tax entirely for items using the template.
 */
@Component({
  selector: 'app-item-tax-template-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-tags me-2"></i>{{ '::ItemTaxTemplates' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (templates().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-tags fa-3x mb-2 d-block opacity-50"></i>
              <p>No item tax templates configured. Create one to override tax rates for specific items.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Title' | abpLocalization }}</th>
                  <th>{{ '::TaxRates' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (t of templates(); track t.id) {
                  <tr [class.table-secondary]="t.isDisabled">
                    <td class="fw-medium">{{ t.title }}</td>
                    <td>
                      @for (d of t.details; track $index) {
                        <span class="badge bg-light text-dark me-1 mb-1">
                          {{ accountLabel(d.taxAccountId) }} {{ d.notApplicable ? '— N/A' : '@ ' + d.taxRate + '%' }}
                        </span>
                      }
                    </td>
                    <td class="text-center">
                      <span class="badge" [class.bg-success]="!t.isDisabled" [class.bg-secondary]="t.isDisabled">
                        {{ t.isDisabled ? 'Disabled' : 'Active' }}
                      </span>
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editTemplate(t)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteTemplate(t.id)" title="Delete"><i class="fas fa-trash"></i></button>
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
            <h6 class="mb-0">{{ editingId() ? ('::EditTemplate' | abpLocalization) : ('::NewTemplate' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-6">
                  <label class="form-label">{{ '::Title' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="title" />
                </div>
                <div class="col-md-3 d-flex align-items-end">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isDisabled" formControlName="isDisabled" />
                    <label class="form-check-label" for="isDisabled">{{ '::Disabled' | abpLocalization }}</label>
                  </div>
                </div>
              </div>

              <h6 class="mt-3">{{ '::TaxRates' | abpLocalization }}</h6>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th style="width:40px">#</th>
                    <th>{{ '::Account' | abpLocalization }}</th>
                    <th>{{ '::Rate' | abpLocalization }} (%)</th>
                    <th class="text-center">{{ '::NotApplicable' | abpLocalization }}</th>
                    <th style="width:50px"></th>
                  </tr>
                </thead>
                <tbody formArrayName="details">
                  @for (row of detailsArray.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td class="text-center text-muted">{{ i + 1 }}</td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="taxAccountId">
                          <option value="">—</option>
                          @for (acc of accounts(); track acc.id) {
                            <option [value]="acc.id">{{ acc.accountCode }} - {{ acc.name }}</option>
                          }
                        </select>
                      </td>
                      <td><input class="form-control form-control-sm" type="number" formControlName="taxRate" /></td>
                      <td class="text-center">
                        <input class="form-check-input" type="checkbox" formControlName="notApplicable" (change)="onNotApplicableChange(i)" />
                      </td>
                      <td>
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeDetail(i)"><i class="fas fa-times"></i></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <button type="button" class="btn btn-outline-secondary btn-sm mb-3" (click)="addDetail()">
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
export class ItemTaxTemplateListComponent implements OnInit {
  private templateService = inject(ItemTaxTemplateService);
  private accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);
  private companyContext = inject(CompanyContextService);

  templates = signal<ItemTaxTemplateRow[]>([]);
  accounts = signal<any[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    title: ['', Validators.required],
    isDisabled: [false],
    details: this.fb.array([]),
  });

  get detailsArray(): FormArray { return this.form.get('details') as FormArray; }

  ngOnInit(): void {
    this.loadTemplates();
    this.loadAccounts();
  }

  loadTemplates(): void {
    this.loading.set(true);
    this.templateService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => { this.templates.set((res.items ?? []) as any); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  loadAccounts(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' } as any).subscribe({
      next: res => this.accounts.set((res.items ?? []).filter((a: any) => a.accountSubType === 5 || a.accountSubType === 6)), // Tax accounts
      error: () => {},
    });
  }

  accountLabel(id: string): string {
    const acc = this.accounts().find(a => a.id === id);
    return acc ? `${acc.accountCode} - ${acc.name}` : id;
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ isDisabled: false });
    this.detailsArray.clear();
    this.addDetail();
    this.showForm.set(true);
  }

  editTemplate(t: ItemTaxTemplateRow): void {
    this.editingId.set(t.id);
    this.form.patchValue({ title: t.title, isDisabled: t.isDisabled });
    this.detailsArray.clear();
    for (const d of t.details) {
      this.detailsArray.push(this.fb.group({
        taxAccountId: [d.taxAccountId, Validators.required],
        taxRate: [d.taxRate],
        notApplicable: [d.notApplicable],
      }));
    }
    this.showForm.set(true);
  }

  addDetail(): void {
    this.detailsArray.push(this.fb.group({
      taxAccountId: ['', Validators.required],
      taxRate: [0],
      notApplicable: [false],
    }));
  }

  removeDetail(index: number): void {
    this.detailsArray.removeAt(index);
  }

  onNotApplicableChange(index: number): void {
    const group = this.detailsArray.at(index);
    if (group.get('notApplicable')?.value) {
      group.get('taxRate')?.setValue(0);
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const companyId = this.companyContext.currentCompanyId();
    const details = (this.form.value.details as any[]).map(d => ({
      taxAccountId: d.taxAccountId,
      taxRate: d.notApplicable ? 0 : d.taxRate,
      notApplicable: d.notApplicable,
    }));

    const request$ = this.editingId()
      ? this.templateService.update(this.editingId()!, {
          title: this.form.value.title!,
          isDisabled: !!this.form.value.isDisabled,
          details,
        })
      : this.templateService.create({
          companyId,
          title: this.form.value.title!,
          details,
        } as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadTemplates();
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

  deleteTemplate(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.templateService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadTemplates(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
