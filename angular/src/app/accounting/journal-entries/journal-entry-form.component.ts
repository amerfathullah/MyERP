import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { AccountDto } from '../../proxy/accounting/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-journal-entry-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    AutoValidationDirective,
    SaveShortcutDirective],
  templateUrl: './journal-entry-form.component.html',
  styleUrls: ['./journal-entry-form.component.scss'],
})
export class JournalEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private journalEntryService = inject(JournalEntryService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  accounts = signal<AccountDto[]>([]);
  companies = signal<any[]>([]);

  form = this.fb.group({
    companyId: [''],
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

    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((res) => this.accounts.set(res.items ?? []));
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));
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

  onAccountSelected(index: number, accountId: string): void {
    const account = this.accounts().find(a => a.id === accountId);
    if (account) {
      this.lines.at(index).get('accountName')?.setValue(account.accountName);
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (!this.isBalanced) {
      this.toaster.error('Journal entry must be balanced (Debit = Credit)');
      return;
    }
    const dto = this.form.getRawValue() as any;
    this.journalEntryService.create(dto).subscribe({
      next: () => {
        this.toaster.success('Journal entry created');
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
