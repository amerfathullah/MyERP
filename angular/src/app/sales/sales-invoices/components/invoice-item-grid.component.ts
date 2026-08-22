import { Component, Input, Output, EventEmitter, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemDetailsService } from '../../../proxy/inventory/item-details.service';
import { TaxCalculationService } from '../../../shared/services/tax-calculation.service';
import { PricingRuleService } from '../../../proxy/sales/pricing-rule.service';
import { ItemPickerComponent } from '../../../shared/components/item-picker/item-picker.component';
import type { ItemDto } from '../../../proxy/inventory/models';

@Component({
  selector: 'app-invoice-item-grid',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    ItemPickerComponent],
  templateUrl: './invoice-item-grid.component.html',
  styleUrls: ['./invoice-item-grid.component.scss'],
})
export class InvoiceItemGridComponent {
  @Input({ required: true }) items!: FormArray;
  @Input() transactionType: string = 'Selling';
  @Input() warehouseId: string = '';
  @Input() companyId: string = '';
  @Input() customerId: string = '';
  @Input() supplierId: string = '';
  @Input() priceListId: string = '';
  @Input() showStockAvailability: boolean = true;
  @Output() rowChanged = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private taxCalc = inject(TaxCalculationService);
  private itemDetailsService = inject(ItemDetailsService);
  private pricingRuleService = inject(PricingRuleService);

  /** Per-item stock availability: { [itemId]: { actualQty, projectedQty } } */
  stockInfo = signal<Record<string, { actualQty: number; projectedQty: number }>>({});

  /** Per-item valuation rate (cost) for margin display */
  valuationRates = signal<Record<string, number>>({});

  /** Per-row Blanket Order hint: { [rowIndex]: { orderNumber, remainingQty } } */
  blanketOrderInfo = signal<Record<number, { orderNumber: string; remainingQty: number }>>({});

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
      blanketOrderId: [null as string | null],
    }));
  }

  onItemSelected(index: number, item: ItemDto | null): void {
    const row = this.items.at(index) as FormGroup;
    const selectedId = item?.id;
    if (selectedId) {
      row.patchValue({ itemName: item!.itemName ?? item!.itemCode ?? '' });

      // Resolve full item details from backend (price, UOM, stock availability)
      this.itemDetailsService.getItemDetails({
        itemId: selectedId,
        transactionType: this.transactionType,
        warehouseId: this.warehouseId || undefined,
        companyId: this.companyId || undefined,
        supplierId: this.supplierId || undefined,
        customerId: this.customerId || undefined,
        priceListId: this.priceListId || undefined,
      }).subscribe({
        next: (details) => {
          if (details) {
            const patch: any = {};
            if (details.rate > 0 && !row.get('rate')?.value) patch.rate = details.rate;
            if (details.description) patch.itemName = details.description;
            patch.blanketOrderId = details.blanketOrderId ?? null;
            row.patchValue(patch);
            this.recalculateRow(index);

            // Capture Blanket Order hint for inline display (rate already applied above)
            if (details.blanketOrderId) {
              this.blanketOrderInfo.update(m => ({
                ...m,
                [index]: {
                  orderNumber: details.blanketOrderNumber ?? '',
                  remainingQty: details.blanketOrderRemainingQty ?? 0,
                },
              }));
            } else {
              this.blanketOrderInfo.update(m => {
                const next = { ...m };
                delete next[index];
                return next;
              });
            }

            // Capture stock availability for inline display
            if (this.showStockAvailability && details.actualQty !== undefined) {
              this.stockInfo.update(m => ({
                ...m,
                [selectedId]: {
                  actualQty: details.actualQty ?? 0,
                  projectedQty: details.projectedQty ?? 0,
                },
              }));
            }

            // Capture valuation rate for margin indicator
            if (details.valuationRate && details.valuationRate > 0) {
              this.valuationRates.update(m => ({ ...m, [selectedId]: details.valuationRate! }));
            }
          }
          // After item details, apply pricing rules (auto-discount/rate)
          this.applyPricingRule(index);
        },
        error: () => {
          // Graceful fallback — still try pricing rules even if item details fail
          this.applyPricingRule(index);
        }
      });
    }
  }

  removeRow(index: number): void {
    this.items.removeAt(index);
    // Blanket Order hints are keyed by row index — shift everything past the removed row down
    this.blanketOrderInfo.update(m => {
      const next: Record<number, { orderNumber: string; remainingQty: number }> = {};
      for (const [key, value] of Object.entries(m)) {
        const i = Number(key);
        if (i < index) next[i] = value;
        else if (i > index) next[i - 1] = value;
      }
      return next;
    });
  }

  getBlanketOrderInfo(index: number): { orderNumber: string; remainingQty: number } | null {
    return this.blanketOrderInfo()[index] ?? null;
  }

  recalculateRow(index: number): void {
    const row = this.items.at(index) as FormGroup;
    const calc = this.taxCalc.calculateItemAmount({
      qty: row.get('qty')!.value ?? 0,
      rate: row.get('rate')!.value ?? 0,
      discountPercent: row.get('discountPercent')!.value ?? 0,
    });
    row.get('amount')!.setValue(calc.amount, { emitEvent: false });
    this.rowChanged.emit();
  }

  getStockForItem(itemId: string): { actualQty: number; projectedQty: number } | null {
    return this.stockInfo()[itemId] ?? null;
  }

  isLowStock(index: number): boolean {
    const row = this.items.at(index) as FormGroup;
    const itemId = row.get('itemId')?.value;
    const qty = row.get('qty')?.value ?? 0;
    const stock = this.stockInfo()[itemId];
    return !!stock && stock.actualQty < qty;
  }

  /** Gross margin % for selling transactions: (rate - cost) / rate × 100 */
  getMarginPercent(index: number): number | null {
    const row = this.items.at(index) as FormGroup;
    const itemId = row.get('itemId')?.value;
    const rate = row.get('rate')?.value ?? 0;
    if (!itemId || rate <= 0) return null;
    const cost = this.valuationRates()[itemId];
    if (cost === undefined || cost <= 0) return null;
    return ((rate - cost) / rate) * 100;
  }

  /** Auto-apply highest-priority matching pricing rule for the item */
  private applyPricingRule(index: number): void {
    const row = this.items.at(index) as FormGroup;
    const itemId = row.get('itemId')?.value;
    const qty = row.get('qty')?.value ?? 1;
    const rate = row.get('rate')?.value ?? 0;
    if (!itemId) return;

    this.pricingRuleService.apply({
      itemId,
      qty,
      amount: qty * rate,
      transactionDate: new Date().toISOString().split('T')[0],
    }).subscribe({
      next: (results) => {
        if (results && results.length > 0) {
          const rule = results[0]; // Highest priority rule (backend returns sorted)
          const patch: any = {};
          if (rule.ruleType === 0 && rule.discountPercentage) {
            // Discount percentage type
            patch.discountPercent = rule.discountPercentage;
          } else if (rule.ruleType === 0 && rule.discountAmount) {
            // Discount amount → convert to effective percentage
            const currentRate = row.get('rate')?.value ?? 0;
            if (currentRate > 0) {
              patch.discountPercent = Math.min(100, (rule.discountAmount / currentRate) * 100);
            }
          } else if (rule.ruleType === 1 && rule.rate) {
            // Rate type — override price directly
            patch.rate = rule.rate;
            patch.discountPercent = 0;
          }
          if (Object.keys(patch).length > 0) {
            row.patchValue(patch);
            this.recalculateRow(index);
          }
        }
      },
      error: () => {} // Non-blocking — pricing rules are advisory
    });
  }
}
