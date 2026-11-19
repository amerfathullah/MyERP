import { Component, Input, Output, EventEmitter, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ItemDetailsService } from '../../../proxy/inventory/item-details.service';
import { TaxCalculationService } from '../../../shared/services/tax-calculation.service';
import { PricingRuleService } from '../../../proxy/sales/pricing-rule.service';
import { BlanketOrderService } from '../../../proxy/sales/blanket-order.service';
import { ItemPickerComponent } from '../../../shared/components/item-picker/item-picker.component';
import { LHDN_CLASSIFICATION_CODES } from '../../../shared/constants/lhdn-classification-codes';
import type { ItemDto } from '../../../proxy/inventory/models';
import type { BlanketOrderDto } from '../../../proxy/sales/models';

@Component({
  selector: 'app-invoice-item-grid',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    ItemPickerComponent],
  templateUrl: './invoice-item-grid.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./invoice-item-grid.component.scss'],
})
export class InvoiceItemGridComponent {
  @Input({ required: true }) items!: FormArray;
  @Input() transactionType: string = 'Selling';
  private _warehouseId: string = '';
  @Input()
  get warehouseId(): string {
    return this._warehouseId;
  }
  set warehouseId(val: string) {
    if (this._warehouseId !== val) {
      this._warehouseId = val;
      this.refreshStockAvailability();
    }
  }
  private _companyId: string = '';
  @Input()
  get companyId(): string { return this._companyId; }
  set companyId(val: string) {
    if (this._companyId !== val) {
      this._companyId = val;
      this.refreshPartyBlanketOrders();
    }
  }

  private _customerId: string = '';
  @Input()
  get customerId(): string { return this._customerId; }
  set customerId(val: string) {
    if (this._customerId !== val) {
      this._customerId = val;
      if (this.transactionType !== 'Buying') {
        this.onPartyChanged();
      }
    }
  }

  private _supplierId: string = '';
  @Input()
  get supplierId(): string { return this._supplierId; }
  set supplierId(val: string) {
    if (this._supplierId !== val) {
      this._supplierId = val;
      if (this.transactionType === 'Buying') {
        this.onPartyChanged();
      }
    }
  }

  private _leadId: string = '';
  @Input()
  get leadId(): string { return this._leadId; }
  set leadId(val: string) {
    if (this._leadId !== val) {
      this._leadId = val;
      if (this.transactionType !== 'Buying' && !this._customerId) {
        this.onPartyChanged();
      }
    }
  }

  private _prospectId: string = '';
  @Input()
  get prospectId(): string { return this._prospectId; }
  set prospectId(val: string) {
    if (this._prospectId !== val) {
      this._prospectId = val;
      if (this.transactionType !== 'Buying' && !this._customerId) {
        this.onPartyChanged();
      }
    }
  }

  @Input() priceListId: string = '';
  @Input() transactionDate: string | Date | null = null;
  @Input() currencyCode: string = '';
  @Input() showStockAvailability: boolean = true;
  @Input() showClassificationCode: boolean = false;
  @Output() rowChanged = new EventEmitter<void>();

  classificationCodes = LHDN_CLASSIFICATION_CODES;

  private fb = inject(FormBuilder);
  private taxCalc = inject(TaxCalculationService);
  private itemDetailsService = inject(ItemDetailsService);
  private pricingRuleService = inject(PricingRuleService);
  private blanketOrderService = inject(BlanketOrderService, { optional: true });

  /** Per-item stock availability: { [itemId]: { actualQty, projectedQty } } */
  stockInfo = signal<Record<string, { actualQty: number; projectedQty: number }>>({});

  /** Per-item valuation rate (cost) for margin display */
  valuationRates = signal<Record<string, number>>({});

  /** Per-row Blanket Order hint: { [rowIndex]: { orderNumber, remainingQty } } */
  blanketOrderInfo = signal<Record<number, { orderNumber: string; remainingQty: number }>>({});

  /** Active Blanket Orders strictly scoped to the current party (ERPNext queries.get_blanket_orders). */
  partyBlanketOrders = signal<BlanketOrderDto[]>([]);

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
      classificationCode: [''],
    }));
  }

  onItemSelected(index: number, item: ItemDto | null): void {
    const row = this.items.at(index) as FormGroup;
    const selectedId = item?.id;
    const hadBlanketOrder = !!row.get('blanketOrderId')?.value;

    // Per ERPNext commits 51df5c2ef0 & f6cca3f59e: clear stale Blanket Order link immediately
    // on item change/clear so an unselected or non-BO item never retains the prior row's BO.
    if (row.get('blanketOrderId')) {
      row.patchValue({ blanketOrderId: null });
    }
    this.blanketOrderInfo.update(m => {
      if (!(index in m)) return m;
      const next = { ...m };
      delete next[index];
      return next;
    });

    if (selectedId) {
      const itemPatch: Record<string, unknown> = { itemName: item!.itemName ?? item!.itemCode ?? '' };
      if (row.get('classificationCode') && !row.get('classificationCode')?.value && item?.lhdnClassificationCode) {
        itemPatch['classificationCode'] = item.lhdnClassificationCode;
      }
      row.patchValue(itemPatch);

      // Resolve full item details from backend (price, UOM, stock availability)
      this.itemDetailsService.getItemDetails({
        itemId: selectedId,
        transactionType: this.transactionType,
        warehouseId: this.warehouseId || undefined,
        companyId: this.companyId || undefined,
        supplierId: this.supplierId || undefined,
        customerId: this.customerId || undefined,
        leadId: this.leadId || undefined,
        prospectId: this.prospectId || undefined,
        priceListId: this.priceListId || undefined,
        transactionDate: this.formatTransactionDate(),
        currencyCode: this.currencyCode || undefined,
      }).subscribe({
        next: (details) => {
          if (details) {
            const patch: any = {};
            // Refresh rate when switching away from a Blanket Order item or when rate is empty
            if (details.rate > 0 && (hadBlanketOrder || !row.get('rate')?.value)) patch.rate = details.rate;
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

  /** Returns active party Blanket Orders containing an open line for the given item (ERPNext a5a5cbbb05). */
  getRowBlanketOrders(itemId: string | null | undefined): BlanketOrderDto[] {
    if (!itemId) return [];
    return this.partyBlanketOrders().filter(bo =>
      (bo.items ?? []).some(i => i.itemId === itemId && !i.isClosed && (i.remainingQty ?? 0) > 0)
    );
  }

  onRowBlanketOrderChanged(index: number, blanketOrderId: string): void {
    const row = this.items.at(index) as FormGroup;
    const itemId = row.get('itemId')?.value;
    if (!blanketOrderId) {
      row.patchValue({ blanketOrderId: null });
      this.blanketOrderInfo.update(m => {
        const next = { ...m };
        delete next[index];
        return next;
      });
      this.rowChanged.emit();
      return;
    }

    const bo = this.partyBlanketOrders().find(b => b.id === blanketOrderId);
    const boItem = bo?.items?.find(i => i.itemId === itemId);
    const patch: Record<string, unknown> = { blanketOrderId };
    if (boItem?.rate && boItem.rate > 0) {
      patch['rate'] = boItem.rate;
    }
    row.patchValue(patch);
    this.blanketOrderInfo.update(m => ({
      ...m,
      [index]: {
        orderNumber: bo?.orderNumber ?? '',
        remainingQty: boItem?.remainingQty ?? 0,
      },
    }));
    this.recalculateRow(index);
  }

  /** Clears stale Blanket Order links and re-resolves BO rates when party changes (ERPNext a5a5cbbb05, 09fd286163). */
  private onPartyChanged(): void {
    this.refreshPartyBlanketOrders();
    if (!this.items?.controls?.length) return;

    this.blanketOrderInfo.set({});
    for (let i = 0; i < this.items.controls.length; i++) {
      const row = this.items.at(i) as FormGroup;
      if (row.get('blanketOrderId')) {
        row.patchValue({ blanketOrderId: null });
      }
      const itemId = row.get('itemId')?.value;
      if (!itemId) continue;
      const partyId = this.transactionType === 'Buying' ? this._supplierId : this._customerId;
      if (!partyId && !this._leadId && !this._prospectId) continue;

      this.itemDetailsService.getItemDetails({
        itemId,
        transactionType: this.transactionType,
        warehouseId: this.warehouseId || undefined,
        companyId: this.companyId || undefined,
        supplierId: this.supplierId || undefined,
        customerId: this.customerId || undefined,
        leadId: this.leadId || undefined,
        prospectId: this.prospectId || undefined,
        priceListId: this.priceListId || undefined,
        transactionDate: this.formatTransactionDate(),
        currencyCode: this.currencyCode || undefined,
      }).subscribe({
        next: (details) => {
          if (!details) return;
          if (details.blanketOrderId) {
            row.patchValue({ blanketOrderId: details.blanketOrderId });
            if (details.rate > 0 && !row.get('rate')?.value) {
              row.patchValue({ rate: details.rate });
              this.recalculateRow(i);
            }
            this.blanketOrderInfo.update(m => ({
              ...m,
              [i]: {
                orderNumber: details.blanketOrderNumber ?? '',
                remainingQty: details.blanketOrderRemainingQty ?? 0,
              },
            }));
          }
        },
        error: () => {},
      });
    }
  }

  private refreshPartyBlanketOrders(): void {
    if (!this.blanketOrderService || !this._companyId) {
      this.partyBlanketOrders.set([]);
      return;
    }
    const orderType = this.transactionType === 'Buying' ? 'Purchasing' : 'Selling';
    const partyId = orderType === 'Purchasing' ? this._supplierId : this._customerId;
    if (!partyId && !(orderType === 'Selling' && (this._leadId || this._prospectId))) {
      this.partyBlanketOrders.set([]);
      return;
    }
    this.blanketOrderService.getActiveForParty({
      companyId: this._companyId,
      orderType,
      partyId: partyId || undefined,
      leadId: !partyId && orderType === 'Selling' ? (this._leadId || undefined) : undefined,
      prospectId: !partyId && orderType === 'Selling' ? (this._prospectId || undefined) : undefined,
      transactionDate: this.formatTransactionDate(),
      currency: this.currencyCode || undefined,
    }).subscribe({
      next: (orders) => this.partyBlanketOrders.set(orders ?? []),
      error: () => this.partyBlanketOrders.set([]),
    });
  }

  private formatTransactionDate(): string | undefined {
    if (!this.transactionDate) return undefined;
    if (this.transactionDate instanceof Date) {
      return Number.isNaN(this.transactionDate.getTime()) ? undefined : this.transactionDate.toISOString().split('T')[0];
    }
    return this.transactionDate || undefined;
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

  refreshStockAvailability(): void {
    if (!this.showStockAvailability || !this.items?.controls?.length) return;
    const itemIds = Array.from(new Set(
      this.items.controls
        .map(c => c.get('itemId')?.value)
        .filter((id): id is string => !!id)
    ));

    for (const itemId of itemIds) {
      this.itemDetailsService.getItemDetails({
        itemId,
        transactionType: this.transactionType,
        warehouseId: this._warehouseId || undefined,
        companyId: this.companyId || undefined,
        supplierId: this.supplierId || undefined,
        customerId: this.customerId || undefined,
        leadId: this.leadId || undefined,
        prospectId: this.prospectId || undefined,
        priceListId: this.priceListId || undefined,
        transactionDate: this.formatTransactionDate(),
        currencyCode: this.currencyCode || undefined,
      }).subscribe({
        next: (details) => {
          if (details && details.actualQty !== undefined) {
            this.stockInfo.update(m => ({
              ...m,
              [itemId]: {
                actualQty: details.actualQty ?? 0,
                projectedQty: details.projectedQty ?? 0,
              },
            }));
          }
          if (details?.valuationRate && details.valuationRate > 0) {
            this.valuationRates.update(m => ({ ...m, [itemId]: details.valuationRate! }));
          }
        },
        error: () => {}
      });
    }
  }
}

