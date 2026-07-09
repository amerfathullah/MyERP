import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LocalizationModule } from '@abp/ng.core';
import { TaxCalculationService } from '../../../shared/services/tax-calculation.service';

@Component({
  selector: 'app-invoice-item-grid',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationModule],
  templateUrl: './invoice-item-grid.component.html',
  styleUrls: ['./invoice-item-grid.component.scss'],
})
export class InvoiceItemGridComponent {
  @Input({ required: true }) items!: FormArray;

  private fb = inject(FormBuilder);
  private taxCalc = inject(TaxCalculationService);

  displayedColumns = ['itemName', 'qty', 'rate', 'discountPercent', 'amount', 'actions'];

  get dataSource(): FormGroup[] {
    return this.items.controls as FormGroup[];
  }

  addRow(): void {
    this.items.push(this.fb.group({
      itemId: [''],
      itemName: ['', Validators.required],
      qty: [1, [Validators.required, Validators.min(0.01)]],
      rate: [0, [Validators.required, Validators.min(0)]],
      discountPercent: [0, [Validators.min(0), Validators.max(100)]],
      amount: [{ value: 0, disabled: true }],
    }));
  }

  removeRow(index: number): void {
    this.items.removeAt(index);
  }

  recalculateRow(index: number): void {
    const row = this.items.at(index) as FormGroup;
    const calc = this.taxCalc.calculateItemAmount({
      qty: row.get('qty')!.value ?? 0,
      rate: row.get('rate')!.value ?? 0,
      discountPercent: row.get('discountPercent')!.value ?? 0,
    });
    row.get('amount')!.setValue(calc.amount, { emitEvent: false });
  }
}
