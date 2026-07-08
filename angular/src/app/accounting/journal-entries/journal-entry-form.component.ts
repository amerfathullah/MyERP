import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';

@Component({
  selector: 'app-journal-entry-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatSelectModule,
  ],
  templateUrl: './journal-entry-form.component.html',
  styleUrls: ['./journal-entry-form.component.scss'],
})
export class JournalEntryFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);

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

  save(): void {
    if (this.form.invalid || !this.isBalanced) return;
    // TODO: Call JournalEntryAppService.create()
    console.log('Saving journal entry:', this.form.getRawValue());
  }

  cancel(): void {
    this.router.navigate(['/accounting/journal-entries']);
  }
}
