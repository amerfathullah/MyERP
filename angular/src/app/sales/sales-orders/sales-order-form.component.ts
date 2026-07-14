import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesOrderStore } from '../store/sales-order.store';
import { CustomerService } from '../../proxy/sales/customer.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-sales-order-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, InvoiceItemGridComponent],
  templateUrl: './sales-order-form.component.html',
  styleUrls: ['./sales-order-form.component.scss'],
})
export class SalesOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private taxCalc = inject(TaxCalculationService);
  private store = inject(SalesOrderStore);
  private customerService = inject(CustomerService);

  customers = signal<any[]>([]);

  form = this.fb.group({
    orderNumber: [''],
    orderDate: [new Date(), Validators.required],
    deliveryDate: [null as Date | null, Validators.required],
    customerId: ['', Validators.required],
    customerName: [''],
    items: this.fb.array([]),
  });

  calcResult: TaxCalculationResult = { netTotal: 0, taxLines: [], totalTax: 0, grandTotal: 0 };

  ngOnInit(): void {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.customers.set(res.items ?? []));
  }

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
    this.router.navigate(['/sales/orders']);
  }

  cancel(): void { this.router.navigate(['/sales/orders']); }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}