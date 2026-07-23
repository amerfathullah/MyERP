import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemDetailsService } from '../../../proxy/inventory/item-details.service';
import { TaxCalculationService } from '../../../shared/services/tax-calculation.service';

@Component({
  selector: 'app-invoice-item-grid',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe],
  templateUrl: './invoice-item-grid.component.html',
  styleUrls: ['./invoice-item-grid.component.scss'],
})
export class InvoiceItemGridComponent {
  @Input({ required: true }) items!: FormArray;
  @Input() availableItems: any[] = [];
  @Input() transactionType: string = 'Selling';
  @Input() warehouseId: string = '';

  private fb = inject(FormBuilder);
  private taxCalc = inject(TaxCalculationService);
  private itemDetailsService = inject(ItemDetailsService);

  displayedColumns = ['itemName', 'qty', 'rate', 'discountPercent', 'amount', 'actions'];

  get dataSource(): FormGroup[] {
    return this.items.controls as FormGroup[];
  }

  addRow(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: [''],
      qty: [1, [Validators.required, Validators.min(0.01)]],
      rate: [0, [Validators.required, Validators.min(0)]],
      discountPercent: [0, [Validators.min(0), Validators.max(100)]],
      amount: [{ value: 0, disabled: true }],
    }));
  }

  onItemSelected(index: number): void {
    const row = this.items.at(index) as FormGroup;
    const selectedId = row.get('itemId')?.value;
    if (selectedId && this.availableItems.length > 0) {
      const item = this.availableItems.find((i: any) => i.id === selectedId);
      if (item) {
        row.patchValue({ itemName: item.itemName ?? item.itemCode ?? '' });
      }

      // Resolve full item details from backend (price, UOM, stock availability)
      this.itemDetailsService.getItemDetails({
        itemId: selectedId,
        transactionType: this.transactionType,
        warehouseId: this.warehouseId || undefined
      }).subscribe({
        next: (details) => {
          if (details) {
            const patch: any = {};
            if (details.rate > 0 && !row.get('rate')?.value) patch.rate = details.rate;
            if (details.description) patch.itemName = details.description;
            if (Object.keys(patch).length > 0) row.patchValue(patch);
          }
        },
        error: () => {} // Graceful fallback — item name already set from local list
      });
    }
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
