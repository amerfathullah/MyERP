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
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

@Component({
  selector: 'app-stock-entry-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatDatepickerModule, MatNativeDateModule, MatButtonModule,
    MatIconModule, MatSelectModule, MatTableModule,
  ],
  templateUrl: './stock-entry-form.component.html',
  styleUrls: ['./stock-entry-form.component.scss'],
})
export class StockEntryFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);

  form = this.fb.group({
    entryType: ['Receipt', Validators.required],
    entryDate: [new Date(), Validators.required],
    sourceWarehouse: [''],
    targetWarehouse: ['', Validators.required],
    remarks: [''],
    items: this.fb.array([]),
  });

  displayedColumns = ['itemName', 'qty', 'uom', 'actions'];

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      qty: [1, [Validators.required, Validators.min(0.01)]],
      uom: ['Unit'],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) return;
    // TODO: Call StockEntryAppService.create()
    console.log('Saving stock entry:', this.form.getRawValue());
  }

  cancel(): void {
    this.router.navigate(['/inventory/items']);
  }
}
