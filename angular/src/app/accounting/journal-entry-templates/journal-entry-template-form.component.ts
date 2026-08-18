import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { JournalEntryTemplateService } from '../../proxy/accounting/journal-entry-template.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { AccountDto } from '../../proxy/accounting/models';

const VOUCHER_TYPES: { value: number; label: string }[] = [
  { value: 0, label: 'Journal Entry' },
  { value: 1, label: 'Inter Company Journal Entry' },
  { value: 2, label: 'Bank Entry' },
  { value: 3, label: 'Cash Entry' },
  { value: 4, label: 'Credit Card Entry' },
  { value: 5, label: 'Debit Note' },
  { value: 6, label: 'Credit Note' },
  { value: 7, label: 'Contra Entry' },
  { value: 8, label: 'Excise Entry' },
  { value: 9, label: 'Write Off Entry' },
  { value: 10, label: 'Opening Entry' },
  { value: 11, label: 'Depreciation Entry' },
  { value: 12, label: 'Exchange Rate Revaluation' },
  { value: 13, label: 'Exchange Gain Or Loss' },
  { value: 14, label: 'Deferred Revenue' },
  { value: 15, label: 'Deferred Expense' },
];

@Component({
  selector: 'app-journal-entry-template-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditJournalEntryTemplate' : 'NewJournalEntryTemplate') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3 mb-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'TemplateName' | abpLocalization }} *</label>
                <input class="form-control" formControlName="templateName" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ 'VoucherType' | abpLocalization }}</label>
                <select class="form-select" formControlName="voucherType">
                  @for (t of voucherTypes; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
                </select>
              </div>
              <div class="col-md-2">
                <div class="form-check mt-4">
                  <input class="form-check-input" type="checkbox" formControlName="isActive" id="isActive" />
                  <label class="form-check-label" for="isActive">{{ 'Active' | abpLocalization }}</label>
                </div>
              </div>
            </div>

            <div class="d-flex justify-content-between align-items-center mb-2">
              <h6 class="mb-0">{{ 'Lines' | abpLocalization }}</h6>
              <button type="button" class="btn btn-sm btn-outline-primary" (click)="addLine()">
                <i class="fa fa-plus me-1"></i>{{ 'AddLine' | abpLocalization }}
              </button>
            </div>
            <table class="table table-sm" formArrayName="lines">
              <thead><tr>
                <th>{{ 'Account' | abpLocalization }}</th>
                <th style="width:110px">{{ 'Debit' | abpLocalization }}/{{ 'Credit' | abpLocalization }}</th>
                <th style="width:140px">{{ 'DefaultAmount' | abpLocalization }}</th>
                <th style="width:120px">{{ 'PartyType' | abpLocalization }}</th>
                <th>{{ 'Description' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (row of lines.controls; track $index) {
                  <tr [formGroupName]="$index">
                    <td>
                      <select class="form-select form-select-sm" formControlName="accountId">
                        <option value="">—</option>
                        @for (a of accounts(); track a.id) { <option [value]="a.id">{{ a.accountCode }} — {{ a.accountName }}</option> }
                      </select>
                    </td>
                    <td>
                      <select class="form-select form-select-sm" formControlName="isDebit">
                        <option [ngValue]="true">{{ 'Debit' | abpLocalization }}</option>
                        <option [ngValue]="false">{{ 'Credit' | abpLocalization }}</option>
                      </select>
                    </td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="defaultAmount" /></td>
                    <td>
                      <select class="form-select form-select-sm" formControlName="partyType">
                        <option [ngValue]="null">—</option>
                        <option value="Customer">{{ 'Customer' | abpLocalization }}</option>
                        <option value="Supplier">{{ 'Supplier' | abpLocalization }}</option>
                      </select>
                    </td>
                    <td><input class="form-control form-control-sm" formControlName="description" /></td>
                    <td><button type="button" class="btn btn-sm btn-outline-danger" (click)="removeLine($index)"><i class="fa fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>

            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || lines.length === 0 || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/accounting/journal-entry-templates">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class JournalEntryTemplateFormComponent implements OnInit {
  private service = inject(JournalEntryTemplateService);
  private accountService = inject(AccountService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  voucherTypes = VOUCHER_TYPES;
  accounts = signal<AccountDto[]>([]);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);

  form = this.fb.group({
    templateName: ['', Validators.required],
    voucherType: [0],
    isActive: [true],
    lines: this.fb.array([]),
  });

  get lines(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  ngOnInit(): void {
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' } as any)
      .subscribe(r => this.accounts.set(r.items ?? []));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(t => {
        this.form.patchValue({ templateName: t.templateName, voucherType: t.voucherType, isActive: t.isActive });
        for (const l of t.lines ?? []) {
          this.addLine(l.accountId, l.isDebit, l.defaultAmount, l.partyType ?? null, l.description ?? '');
        }
      });
    }
  }

  addLine(accountId = '', isDebit = true, defaultAmount = 0, partyType: string | null = null, description = ''): void {
    this.lines.push(this.fb.group({
      accountId: [accountId, Validators.required],
      isDebit: [isDebit],
      defaultAmount: [defaultAmount],
      partyType: [partyType],
      description: [description],
    }));
  }

  removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  save(): void {
    if (this.form.invalid || this.lines.length === 0) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue() as any;
    const dto = { ...val, companyId: this.companyContext.currentCompanyId() };
    const req$ = this.isEdit() ? this.service.update(this.editId()!, dto) : this.service.create(dto);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/accounting/journal-entry-templates']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
