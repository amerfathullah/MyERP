import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';
import { JournalEntryTemplateService } from '../../proxy/accounting/journal-entry-template.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { AccountDto, JournalEntryTemplateDto } from '../../proxy/accounting/models';

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
  styleUrls: ['./journal-entry-form.component.scss'],
})
export class JournalEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private journalEntryService = inject(JournalEntryService);
  private templateService = inject(JournalEntryTemplateService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  companies = signal<any[]>([]);
  templates = signal<JournalEntryTemplateDto[]>([]);
  voucherTypes = VOUCHER_TYPES;

  form = this.fb.group({
    companyId: [''],
    voucherType: [0],
    entryDate: [new Date(), Validators.required],
    reference: [''],
    narration: [''],
    lines: this.fb.array([]),
  });
  get lines(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  get totalDebit(): number {
    return this.lines.controls.reduce((sum, c) => sum + (c.get('debit')?.value || 0), 0);
  }

  get totalCredit(): number {
    return this.lines.controls.reduce((sum, c) => sum + (c.get('credit')?.value || 0), 0);
  }

  get isBalanced(): boolean {
    return Math.abs(this.totalDebit - this.totalCredit) < 0.01;
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
          debit: [line.isDebit ? (line.defaultAmount ?? 0) : 0, [Validators.min(0)]],
          credit: [!line.isDebit ? (line.defaultAmount ?? 0) : 0, [Validators.min(0)]],
        }));
      }
    });
  }

  addLine(): void {
    this.lines.push(this.fb.group({
      accountId: ['', Validators.required],
      accountName: [''],
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
    this.lines.at(index).get('accountName')?.setValue(account?.accountName ?? '');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (!this.isBalanced) {
      this.toaster.error('::JournalEntryMustBeBalanced');
      return;
    }
    const dto = this.form.getRawValue() as any;
    this.journalEntryService.create(dto).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/accounting/journal-entries']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  cancel(): void {
    this.router.navigate(['/accounting/journal-entries']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
