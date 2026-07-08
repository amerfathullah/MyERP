import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';

@Component({
  selector: 'app-sales-order-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, InvoiceItemGridComponent],
  templateUrl: './sales-order-form.component.html',
  styleUrls: ['./sales-order-form.component.scss'],
})
export class SalesOrderFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private taxCalc = inject(TaxCalculationService);

  form = this.fb.group({
    orderNumber: [''],
    orderDate: [new Date(), Validators.required],
    deliveryDate: [null as Date | null, Validators.required],
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
    // TODO: Call SalesOrderAppService.create()
    console.log('Saving sales order:', this.form.getRawValue(), this.calcResult);
  }

  cancel(): void { this.router.navigate(['/sales/orders']); }
}
