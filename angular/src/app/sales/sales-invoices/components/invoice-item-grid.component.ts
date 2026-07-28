import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemDetailsService } from '../../../proxy/inventory/item-details.service';
import { TaxCalculationService } from '../../../shared/services/tax-calculation.service';
import { PricingRuleService } from '../../../proxy/sales/pricing-rule.service';

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
  @Input() companyId: string = '';
  @Input() customerId: string = '';
  @Input() supplierId: string = '';
  @Output() rowChanged = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private taxCalc = inject(TaxCalculationService);
  private itemDetailsService = inject(ItemDetailsService);
  private pricingRuleService = inject(PricingRuleService);

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
        warehouseId: this.warehouseId || undefined,
        companyId: this.companyId || undefined,
        supplierId: this.supplierId || undefined,
        customerId: this.customerId || undefined,
      }).subscribe({
        next: (details) => {
          if (details) {
            const patch: any = {};
            if (details.rate > 0 && !row.get('rate')?.value) patch.rate = details.rate;
            if (details.description) patch.itemName = details.description;
            if (Object.keys(patch).length > 0) row.patchValue(patch);
            this.recalculateRow(index);
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
