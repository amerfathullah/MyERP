import { Injectable } from '@angular/core';

export interface InvoiceItemCalc {
  qty: number;
  rate: number;
  discountPercent: number;
  discountAmount: number;
  amount: number;
}

export interface TaxLine {
  taxName: string;
  rate: number;
  chargeType: 'OnNetTotal' | 'OnPreviousRowTotal' | 'Actual';
  taxAmount: number;
  total: number;
}

export interface TaxRuleDto {
  taxName: string;
  rate: number;
  chargeType: 'OnNetTotal' | 'OnPreviousRowTotal' | 'Actual';
  actualAmount?: number;
}

export interface TaxCalculationResult {
  netTotal: number;
  taxLines: TaxLine[];
  totalTax: number;
  grandTotal: number;
}

/**
 * Replaces erpnext/public/js/controllers/taxes_and_totals.js
 * Handles item amount calculation, tax cascade, and grand total computation.
 */
@Injectable({ providedIn: 'root' })
export class TaxCalculationService {

  /**
   * Calculate individual item amounts.
   * amount = (qty * rate) - discount
   */
  calculateItemAmount(item: { qty: number; rate: number; discountPercent?: number }): InvoiceItemCalc {
    const grossAmount = item.qty * item.rate;
    const discountPercent = item.discountPercent ?? 0;
    const discountAmount = grossAmount * (discountPercent / 100);
    const amount = grossAmount - discountAmount;

    return {
      qty: item.qty,
      rate: item.rate,
      discountPercent,
      discountAmount: Math.round(discountAmount * 100) / 100,
      amount: Math.round(amount * 100) / 100,
    };
  }

  /**
   * Calculate net total from all line items.
   */
  calculateNetTotal(items: Array<{ qty: number; rate: number; discountPercent?: number }>): number {
    return items.reduce((sum, item) => {
      const calc = this.calculateItemAmount(item);
      return sum + calc.amount;
    }, 0);
  }

  /**
   * Calculate taxes using cascading logic (mirrors taxes_and_totals.js calculate_taxes).
   * Supports:
   * - OnNetTotal: percentage of net total
   * - OnPreviousRowTotal: percentage of previous tax row's running total
   * - Actual: fixed amount
   */
  calculateTaxes(netTotal: number, taxRules: TaxRuleDto[]): TaxLine[] {
    const taxLines: TaxLine[] = [];
    let runningTotal = netTotal;

    for (const rule of taxRules) {
      let taxAmount: number;

      switch (rule.chargeType) {
        case 'OnNetTotal':
          taxAmount = netTotal * (rule.rate / 100);
          break;
        case 'OnPreviousRowTotal':
          taxAmount = runningTotal * (rule.rate / 100);
          break;
        case 'Actual':
          taxAmount = rule.actualAmount ?? 0;
          break;
        default:
          taxAmount = 0;
      }

      taxAmount = Math.round(taxAmount * 100) / 100;
      runningTotal += taxAmount;

      taxLines.push({
        taxName: rule.taxName,
        rate: rule.rate,
        chargeType: rule.chargeType,
        taxAmount,
        total: Math.round(runningTotal * 100) / 100,
      });
    }

    return taxLines;
  }

  /**
   * Full calculation: items → net total → taxes → grand total.
   */
  calculate(
    items: Array<{ qty: number; rate: number; discountPercent?: number }>,
    taxRules: TaxRuleDto[],
  ): TaxCalculationResult {
    const netTotal = this.calculateNetTotal(items);
    const taxLines = this.calculateTaxes(netTotal, taxRules);
    const totalTax = taxLines.reduce((sum, t) => sum + t.taxAmount, 0);
    const grandTotal = Math.round((netTotal + totalTax) * 100) / 100;

    return {
      netTotal: Math.round(netTotal * 100) / 100,
      taxLines,
      totalTax: Math.round(totalTax * 100) / 100,
      grandTotal,
    };
  }
}
