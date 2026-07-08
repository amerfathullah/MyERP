import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';

@Component({
  selector: 'app-quotation-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule,
    MatCardModule,
    MatDatepickerModule, MatNativeDateModule, InvoiceItemGridComponent,
  ],
  templateUrl: './quotation-form.component.html',
  styleUrls: ['./quotation-form.component.scss'],
})
export class QuotationFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private taxCalc = inject(TaxCalculationService);

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
    // TODO: Call QuotationAppService.create()
    console.log('Saving quotation:', this.form.getRawValue(), this.calcResult);
  }

  cancel(): void { this.router.navigate(['/sales/quotations']); }
}
