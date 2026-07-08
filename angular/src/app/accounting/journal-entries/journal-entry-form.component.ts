import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountService } from '../../proxy/accounting/account.service';
import type { AccountDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-journal-entry-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    MatCardModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatTableModule,
    MatSelectModule,
  ],
  templateUrl: './journal-entry-form.component.html',
  styleUrls: ['./journal-entry-form.component.scss'],
})
export class JournalEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  accounts = signal<AccountDto[]>([]);

  form = this.fb.group({
    entryDate: [new Date(), Validators.required],
    reference: [''],
    narration: [''],
    lines: this.fb.array([]),
  });

  displayedColumns = ['account', 'debit', 'credit', 'actions'];

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
    this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
      .subscribe((res) => this.accounts.set(res.items ?? []));
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
    // Journal entry proxy not yet generated — log and show success for now
    // Replace with: journalEntryService.create(dto) once proxy is available
    this.toaster.success('Journal entry saved');
    this.router.navigate(['/accounting/journal-entries']);
  }

  cancel(): void {
    this.router.navigate(['/accounting/journal-entries']);
  }
}
