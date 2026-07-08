import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { InvoiceItemGridComponent } from './components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';

@Component({
  selector: 'app-sales-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    InvoiceItemGridComponent,
  ],
  templateUrl: './sales-invoice-form.component.html',
  styleUrls: ['./sales-invoice-form.component.scss'],
})
export class SalesInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private taxCalc = inject(TaxCalculationService);

  form = this.fb.group({
    invoiceNumber: ['', Validators.required],
    issueDate: [new Date(), Validators.required],
    customerId: ['', Validators.required],
    customerName: [''],
    buyerTin: ['', Validators.required],
    currency: ['MYR'],
    items: this.fb.array([]),
  });

  calcResult: TaxCalculationResult = {
    netTotal: 0,
    taxLines: [],
    totalTax: 0,
    grandTotal: 0,
  };

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    // TODO: If editing, load invoice by route param ID
  }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('qty')?.value ?? 0,
      rate: c.get('rate')?.value ?? 0,
      discountPercent: c.get('discountPercent')?.value ?? 0,
    }));

    // TODO: Load tax rules from backend based on company/customer
    this.calcResult = this.taxCalc.calculate(itemValues, []);
  }

  save(): void {
    if (this.form.invalid) return;
    this.recalculate();
    // TODO: Call SalesInvoiceAppService.create() or .update()
    console.log('Saving invoice:', this.form.getRawValue(), this.calcResult);
  }

  cancel(): void {
    this.router.navigate(['/sales/invoices']);
  }
}
