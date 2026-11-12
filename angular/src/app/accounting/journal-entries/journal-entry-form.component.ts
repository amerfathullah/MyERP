import { ChangeDetectorRef, Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';
import { JournalEntryTemplateService } from '../../proxy/accounting/journal-entry-template.service';
import { FiscalYearService } from '../../proxy/accounting/fiscal-year.service';
import { CompanyService } from '../../proxy/core/company.service';
import type {
  AccountDto,
  CreateJournalEntryDto,
  CreateJournalEntryLineDto,
  JournalEntryTemplateDto,
} from '../../proxy/accounting/models';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { LinkPickerComponent } from '../../shared/components/link-picker/link-picker.component';
import { map, Observable } from 'rxjs';

/**
 * Journal Entry voucher types — per ERPNext's 18 voucher_type values.
 * Each type determines validation rules and which accounts can be debited/credited.
 */
const VOUCHER_TYPES: { value: number; key: string; label: string }[] = [
  { value: 0, key: 'JournalEntry', label: 'Journal Entry' },
  { value: 1, key: 'InterCompanyJournalEntry', label: 'Inter Company Journal Entry' },
  { value: 2, key: 'BankEntry', label: 'Bank Entry' },
  { value: 3, key: 'CashEntry', label: 'Cash Entry' },
  { value: 4, key: 'CreditCardEntry', label: 'Credit Card Entry' },
  { value: 5, key: 'DebitNote', label: 'Debit Note' },
  { value: 6, key: 'CreditNote', label: 'Credit Note' },
  { value: 7, key: 'ContraEntry', label: 'Contra Entry' },
  { value: 8, key: 'ExciseEntry', label: 'Excise Entry' },
  { value: 9, key: 'WriteOffEntry', label: 'Write Off Entry' },
  { value: 10, key: 'OpeningEntry', label: 'Opening Entry' },
  { value: 11, key: 'DepreciationEntry', label: 'Depreciation Entry' },
  { value: 12, key: 'ExchangeRateRevaluation', label: 'Exchange Rate Revaluation' },
  { value: 13, key: 'ExchangeGainOrLoss', label: 'Exchange Gain Or Loss' },
  { value: 14, key: 'DeferredRevenue', label: 'Deferred Revenue' },
  { value: 15, key: 'DeferredExpense', label: 'Deferred Expense' },
];

@Component({
  selector: 'app-journal-entry-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    AutoValidationDirective,
    SaveShortcutDirective,
    LinkPickerComponent],
  templateUrl: './journal-entry-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./journal-entry-form.component.scss'],
})
export class JournalEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private journalEntryService = inject(JournalEntryService);
  private templateService = inject(JournalEntryTemplateService);
  private companyService = inject(CompanyService);
  private fiscalYearService = inject(FiscalYearService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private cdr = inject(ChangeDetectorRef);

  companies = signal<CompanyDto[]>([]);
  saving = signal(false);
  templates = signal<JournalEntryTemplateDto[]>([]);
  voucherTypes = VOUCHER_TYPES;

  form = this.fb.group({
    companyId: [''],
    voucherType: [0],
    entryDate: [new Date().toISOString().split('T')[0], Validators.required],
    reference: [''],
    narration: [''],
    // Per ERPNext Journal Entry: rows on an account in another currency need this switched on.
    isMultiCurrency: [false],
    lines: this.fb.array([]),
  });
  get lines(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  /** Totals are in company currency: each row's amount in its account currency times its rate. */
  get totalDebit(): number {
    return Math.round(this.lines.controls.reduce((sum, _, i) => sum + this.companyAmount(i, 'debit'), 0) * 100) / 100;
  }

  get totalCredit(): number {
    return Math.round(this.lines.controls.reduce((sum, _, i) => sum + this.companyAmount(i, 'credit'), 0) * 100) / 100;
  }

  get companyCurrency(): string {
    const companyId = this.form.get('companyId')?.value;
    return this.companies().find(c => c.id === companyId)?.currencyCode || '';
  }

  get isMultiCurrency(): boolean {
    return !!this.form.get('isMultiCurrency')?.value;
  }

  /** A row is foreign when its account's currency is known and differs from the company's. */
  isForeignRow(index: number): boolean {
    const currency = this.lines.at(index).get('accountCurrency')?.value;
    return !!currency && !!this.companyCurrency && currency !== this.companyCurrency;
  }

  /** Company currency value of a row's debit or credit (ERPNext: amount in account currency x exchange rate). */
  companyAmount(index: number, side: 'debit' | 'credit'): number {
    const row = this.lines.at(index);
    const amount = Number(row.get(side)?.value) || 0;
    const rate = this.isForeignRow(index) ? Number(row.get('exchangeRate')?.value) || 0 : 1;
    return Math.round(amount * rate * 100) / 100;
  }

  get difference(): number {
    return Math.round((this.totalDebit - this.totalCredit) * 100) / 100;
  }

  get isBalanced(): boolean {
    return Math.abs(this.difference) < 0.01;
  }

  ngOnInit(): void {
    const cid = this.companyContext.currentCompanyId();
    if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe({ next: (res) => this.companies.set(res.items ?? []), error: () => {} });

    if (cid) {
      this.templateService.getList({ companyId: cid, maxResultCount: 100 } as any)
        .subscribe({ next: (res) => this.templates.set((res.items ?? []).filter(t => t.isActive)), error: () => {} });
    }
  }

  loadTemplate(templateId: string): void {
    if (!templateId) return;
    this.templateService.get(templateId).subscribe(t => {
      this.form.patchValue({ voucherType: t.voucherType });
      while (this.lines.length) this.lines.removeAt(0);
      for (const line of t.lines ?? []) {
        this.lines.push(this.fb.group({
          accountId: [line.accountId, Validators.required],
          accountName: [line.accountName ?? ''],
          accountCurrency: [''],
          exchangeRate: [1, [Validators.min(0)]],
          debit: [line.isDebit ? (line.defaultAmount ?? 0) : 0, [Validators.min(0)]],
          credit: [!line.isDebit ? (line.defaultAmount ?? 0) : 0, [Validators.min(0)]],
        }));
        if (line.accountId) this.loadAccountDetails(this.lines.length - 1, line.accountId);
      }
    });
  }

  addLine(): void {
    this.lines.push(this.fb.group({
      accountId: ['', Validators.required],
      accountName: [''],
      accountCurrency: [''],
      exchangeRate: [1, [Validators.min(0)]],
      debit: [0, [Validators.min(0)]],
      credit: [0, [Validators.min(0)]],
    }));
  }

  removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  accountSearchFn = (filter: string): Observable<AccountDto[]> =>
    this.accountService.getList({ filter, skipCount: 0, maxResultCount: 20, sorting: '' } as any)
      .pipe(map(res => res.items ?? []));
  accountGetByIdFn = (id: string) => this.accountService.get(id);
  accountDisplayFn = (a: AccountDto | null) => a ? `${a.accountCode} — ${a.accountName}` : '';

  onAccountLinkSelected(index: number, account: AccountDto | null): void {
    const row = this.lines.at(index);
    row.get('accountName')?.setValue(account?.accountName ?? '');
    if (account?.id) {
      this.loadAccountDetails(index, account.id);
    } else {
      row.patchValue({ accountCurrency: '', exchangeRate: 1 });
    }
  }

  /**
   * Per ERPNext get_account_details_and_party_type: picking an account brings its currency and
   * the exchange rate for the posting date. A foreign currency account switches Multi Currency on.
   */
  private loadAccountDetails(index: number, accountId: string): void {
    const { companyId, entryDate } = this.form.getRawValue();
    if (!companyId || !entryDate) return;
    this.journalEntryService.getAccountDetails(companyId, accountId, entryDate).subscribe({
      next: details => {
        const row = this.lines.at(index);
        if (!row || row.get('accountId')?.value !== accountId) return;
        row.patchValue({ accountCurrency: details.accountCurrency ?? '', exchangeRate: details.exchangeRate ?? 1 });
        if (details.accountCurrency && details.accountCurrency !== details.companyCurrency) {
          this.form.patchValue({ isMultiCurrency: true });
        }
        // The form was patched from an HTTP callback; the grid columns and totals read it through getters.
        this.cdr.markForCheck();
      },
      error: () => {},
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const rows = raw.lines as { accountId: string; debit: number; credit: number; exchangeRate: number }[];
    if (rows.some(r => (Number(r.debit) || 0) > 0 && (Number(r.credit) || 0) > 0)) {
      this.toaster.error('::CannotDebitAndCreditSameRow');
      return;
    }
    if (rows.some(r => !(Number(r.debit) || 0) && !(Number(r.credit) || 0))) {
      this.toaster.error('::EveryRowNeedsDebitOrCredit');
      return;
    }
    if (!this.isBalanced) {
      this.toaster.error('::JournalEntryMustBeBalanced');
      return;
    }

    const companyId = raw.companyId ?? '';
    const postingDate = raw.entryDate ?? '';
    this.saving.set(true);
    this.fiscalYearService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: res => {
        const day = postingDate.substring(0, 10);
        const fiscalYear = (res.items ?? []).find(fy =>
          fy.companyId === companyId
          && (fy.startDate ?? '').substring(0, 10) <= day
          && (fy.endDate ?? '').substring(0, 10) >= day);
        if (!fiscalYear?.id) {
          this.saving.set(false);
          this.toaster.error('::NoFiscalYearForPostingDate');
          return;
        }

        const lines: CreateJournalEntryLineDto[] = rows.map((r, index) => {
          const isDebit = (Number(r.debit) || 0) > 0;
          const amount = Number(isDebit ? r.debit : r.credit);
          // A foreign currency row sends what was entered: the account currency amount and its rate.
          return this.isForeignRow(index)
            ? { accountId: r.accountId, isDebit, amountInAccountCurrency: amount, exchangeRate: Number(r.exchangeRate) || null }
            : { accountId: r.accountId, isDebit, amount };
        });
        const dto: CreateJournalEntryDto = {
          companyId,
          fiscalYearId: fiscalYear.id,
          postingDate,
          voucherType: raw.voucherType ?? 0,
          referenceNumber: raw.reference || null,
          narration: raw.narration || null,
          isMultiCurrency: !!raw.isMultiCurrency,
          lines,
        };

        this.journalEntryService.create(dto).subscribe({
          next: () => {
            this.saving.set(false);
            this.form.markAsPristine();
            this.toaster.success('::SuccessfullyCreated');
            this.router.navigate(['/accounting/journal-entries']);
          },
          error: (err: any) => {
            this.saving.set(false);
            this.toaster.error(err?.error?.error?.message ?? '::CreateFailed');
          },
        });
      },
      error: () => this.saving.set(false),
    });
  }

  cancel(): void {
    this.router.navigate(['/accounting/journal-entries']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
