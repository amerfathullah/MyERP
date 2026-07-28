import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface TaxTemplate {
  id: string;
  companyId: string;
  name: string;
  templateType: number;
  isDefault: boolean;
  isEnabled: boolean;
  rows: TaxRow[];
}

interface TaxRow {
  id?: string;
  rowIndex: number;
  chargeType: string;
  rate: number;
  accountId: string | null;
  accountName: string | null;
  taxCategory: string;
  referenceRowIndex: number | null;
  includedInPrintRate: boolean;
  description: string | null;
}

/**
 * Tax Charges Template Management — configure reusable tax definitions for transactions.
 * Per ERPNext: Sales Taxes and Charges Template / Purchase Taxes and Charges Template.
 * 
 * Features:
 * - List all templates (selling/buying) with enable/disable toggle
 * - Create/edit template with dynamic row management
 * - Set default template per company
 * - Charge type selection (On Net Total, On Previous Row, Actual, etc.)
 * - Account assignment per tax row
 */
@Component({
  selector: 'app-tax-template-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-percent me-2"></i>{{ 'Tax::TaxChargesTemplates' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <!-- Filters -->
        <div class="card-body border-bottom bg-light">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small">{{ '::Type' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="filterType" (ngModelChange)="loadTemplates()">
                <option value="">All Types</option>
                <option value="0">Selling</option>
                <option value="1">Buying</option>
              </select>
            </div>
          </div>
        </div>

        <!-- Template List -->
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (templates().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-percent fa-3x mb-2 d-block opacity-50"></i>
              <p>No tax templates configured. Create one to auto-populate taxes on transactions.</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::Type' | abpLocalization }}</th>
                  <th>{{ '::TaxRows' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Default' | abpLocalization }}</th>
                  <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                  <th style="width:120px"></th>
                </tr>
              </thead>
              <tbody>
                @for (t of templates(); track t.id) {
                  <tr [class.table-secondary]="!t.isEnabled">
                    <td class="fw-medium">{{ t.name }}</td>
                    <td><span class="badge" [class.bg-success]="t.templateType === 0" [class.bg-info]="t.templateType === 1">{{ t.templateType === 0 ? 'Selling' : 'Buying' }}</span></td>
                    <td>
                      @for (row of t.rows; track row.rowIndex) {
                        <span class="badge bg-light text-dark me-1 mb-1">{{ row.description || row.chargeType }} @ {{ row.rate }}%</span>
                      }
                    </td>
                    <td class="text-center">
                      @if (t.isDefault) { <i class="fas fa-star text-warning"></i> }
                    </td>
                    <td class="text-center">
                      <span class="badge" [class.bg-success]="t.isEnabled" [class.bg-secondary]="!t.isEnabled">
                        {{ t.isEnabled ? 'Active' : 'Disabled' }}
                      </span>
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-primary" (click)="editTemplate(t)" title="Edit"><i class="fas fa-edit"></i></button>
                        <button class="btn btn-outline-secondary" (click)="toggleEnabled(t)" [title]="t.isEnabled ? 'Disable' : 'Enable'">
                          <i class="fas" [class.fa-toggle-on]="t.isEnabled" [class.fa-toggle-off]="!t.isEnabled"></i>
                        </button>
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

      <!-- Create/Edit Form -->
      @if (showForm()) {
        <div class="card mt-3">
          <div class="card-header">
            <h6 class="mb-0">{{ editingId() ? ('::EditTemplate' | abpLocalization) : ('::NewTemplate' | abpLocalization) }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
                  <input class="form-control" formControlName="name" placeholder="e.g., Malaysia SST 6%" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Type' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="templateType">
                    <option [value]="0">Selling</option>
                    <option [value]="1">Buying</option>
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::TaxCategory' | abpLocalization }}</label>
                  <select class="form-select" formControlName="taxCategoryId">
                    <option value="">—</option>
                    @for (cat of taxCategories(); track cat.id) {
                      <option [value]="cat.id">{{ cat.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-2 d-flex align-items-end">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isDefault" formControlName="isDefault" />
                    <label class="form-check-label" for="isDefault">Default</label>
                  </div>
                </div>
              </div>

              <!-- Tax Rows -->
              <h6 class="mt-3">Tax Rows</h6>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th style="width:40px">#</th>
                    <th>{{ '::ChargeType' | abpLocalization }}</th>
                    <th>{{ '::Rate' | abpLocalization }} (%)</th>
                    <th>{{ '::Description' | abpLocalization }}</th>
                    <th>{{ '::Category' | abpLocalization }}</th>
                    <th>{{ '::Account' | abpLocalization }}</th>
                    <th style="width:50px"></th>
                  </tr>
                </thead>
                <tbody formArrayName="rows">
                  @for (row of rowsArray.controls; track $index; let i = $index) {
                    <tr [formGroupName]="i">
                      <td class="text-center text-muted">{{ i + 1 }}</td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="chargeType">
                          <option value="On Net Total">On Net Total</option>
                          <option value="On Previous Row Amount">On Previous Row Amount</option>
                          <option value="On Previous Row Total">On Previous Row Total</option>
                          <option value="On Item Quantity">On Item Quantity</option>
                          <option value="Actual">Actual</option>
                        </select>
                      </td>
                      <td><input class="form-control form-control-sm" type="number" formControlName="rate" /></td>
                      <td><input class="form-control form-control-sm" formControlName="description" placeholder="e.g., SST @ 6%" /></td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="taxCategory">
                          <option value="Total">Total</option>
                          <option value="Valuation">Valuation</option>
                          <option value="Valuation and Total">Valuation and Total</option>
                        </select>
                      </td>
                      <td>
                        <select class="form-select form-select-sm" formControlName="accountId">
                          <option value="">—</option>
                          @for (acc of accounts(); track acc.id) {
                            <option [value]="acc.id">{{ acc.accountCode }} - {{ acc.name }}</option>
                          }
                        </select>
                      </td>
                      <td>
                        <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeRow(i)"><i class="fas fa-times"></i></button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <button type="button" class="btn btn-outline-secondary btn-sm mb-3" (click)="addRow()">
                <i class="fas fa-plus me-1"></i>Add Tax Row
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
export class TaxChargesTemplateListComponent implements OnInit {
  private http = inject(HttpClient);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  templates = signal<TaxTemplate[]>([]);
  taxCategories = signal<any[]>([]);
  accounts = signal<any[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);
  filterType = '';

  form = this.fb.group({
    name: ['', Validators.required],
    templateType: [0, Validators.required],
    taxCategoryId: [''],
    isDefault: [false],
    rows: this.fb.array([]),
  });

  get rowsArray(): FormArray { return this.form.get('rows') as FormArray; }

  ngOnInit(): void {
    this.loadTemplates();
    this.loadTaxCategories();
    this.loadAccounts();
  }

  loadTemplates(): void {
    this.loading.set(true);
    const params: any = { skipCount: '0', maxResultCount: '100' };
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) params.companyId = companyId;
    if (this.filterType) params.templateType = this.filterType;

    this.http.get<any>('/api/app/tax-charges-template', { params }).subscribe({
      next: res => { this.templates.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  loadTaxCategories(): void {
    this.http.get<any>('/api/app/tax-category', { params: { skipCount: '0', maxResultCount: '100' } }).subscribe({
      next: res => this.taxCategories.set(res.items ?? []),
      error: () => {},
    });
  }

  loadAccounts(): void {
    this.http.get<any>('/api/app/account', { params: { skipCount: '0', maxResultCount: '500', sorting: 'accountCode asc' } }).subscribe({
      next: res => this.accounts.set((res.items ?? []).filter((a: any) => a.accountSubType === 5 || a.accountSubType === 6)), // Tax accounts
      error: () => {},
    });
  }

  openForm(): void {
    this.editingId.set(null);
    this.form.reset({ templateType: 0, isDefault: false });
    this.rowsArray.clear();
    this.addRow(); // Start with one row
    this.showForm.set(true);
  }

  editTemplate(t: TaxTemplate): void {
    this.editingId.set(t.id);
    this.form.patchValue({
      name: t.name,
      templateType: t.templateType,
      taxCategoryId: (t as any).taxCategoryId || '',
      isDefault: t.isDefault,
    });
    this.rowsArray.clear();
    for (const row of t.rows) {
      this.rowsArray.push(this.fb.group({
        chargeType: [row.chargeType],
        rate: [row.rate],
        description: [row.description || ''],
        taxCategory: [row.taxCategory || 'Total'],
        accountId: [row.accountId || ''],
        referenceRowIndex: [row.referenceRowIndex],
        includedInPrintRate: [row.includedInPrintRate],
      }));
    }
    this.showForm.set(true);
  }

  addRow(): void {
    this.rowsArray.push(this.fb.group({
      chargeType: ['On Net Total'],
      rate: [0],
      description: [''],
      taxCategory: ['Total'],
      accountId: [''],
      referenceRowIndex: [null],
      includedInPrintRate: [false],
    }));
  }

  removeRow(index: number): void {
    this.rowsArray.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const companyId = this.companyContext.currentCompanyId();
    const payload = {
      ...this.form.value,
      companyId,
      templateType: Number(this.form.value.templateType),
      taxCategoryId: this.form.value.taxCategoryId || null,
      rows: (this.form.value.rows as any[]).map((r, i) => ({
        ...r,
        accountId: r.accountId || null,
        referenceRowIndex: r.referenceRowIndex ?? null,
      })),
    };

    const request$ = this.editingId()
      ? this.http.put(`/api/app/tax-charges-template/${this.editingId()}`, payload)
      : this.http.post('/api/app/tax-charges-template', payload);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? 'Template updated.' : 'Template created.');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadTemplates();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || 'Failed to save template.');
        this.saving.set(false);
      },
    });
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  toggleEnabled(t: TaxTemplate): void {
    this.http.post(`/api/app/tax-charges-template/${t.id}/toggle-enabled`, {}).subscribe({
      next: () => { this.loadTemplates(); },
      error: () => this.toaster.error('Failed to toggle template.'),
    });
  }

  deleteTemplate(id: string): void {
    if (!confirm('Delete this template?')) return;
    this.http.delete(`/api/app/tax-charges-template/${id}`).subscribe({
      next: () => { this.toaster.success('Template deleted.'); this.loadTemplates(); },
      error: () => this.toaster.error('Failed to delete template.'),
    });
  }
}
