import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { QuotationStore } from '../store/quotation.store';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-quotation-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, InvoiceItemGridComponent],
  templateUrl: './quotation-form.component.html',
  styleUrls: ['./quotation-form.component.scss'],
})
export class QuotationFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private taxCalc = inject(TaxCalculationService);
  private store = inject(QuotationStore);

  form = this.fb.group({
    quotationNumber: [''],
    quotationDate: [new Date(), Validators.required],
    validUntil: [null as Date | null],
    customerId: ['', Validators.required],
    customerName: [''],
    items: this.fb.array([]),
  });

  calcResult: TaxCalculationResult = { netTotal: 0, taxLines: [], totalTax: 0, grandTotal: 0 };

  get items(): FormArray { return this.form.get('items') as FormArray; }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('qty')?.value ?? 0,
      rate: c.get('rate')?.value ?? 0,
      discountPercent: c.get('discountPercent')?.value ?? 0,
    }));
    this.calcResult = this.taxCalc.calculate(itemValues, []);
  }

  save(): void {
    if (this.form.invalid) return;
    this.recalculate();
    const dto = this.form.getRawValue() as any;
    this.store.create(dto);
    this.router.navigate(['/sales/quotations']);
  }

  cancel(): void { this.router.navigate(['/sales/quotations']); }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}