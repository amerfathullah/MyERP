import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { InvoiceItemGridComponent } from './components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import type { CreateSalesInvoiceDto, SalesInvoiceItemDto } from '../../proxy/sales/models';

@Component({
  selector: 'app-sales-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    InvoiceItemGridComponent],
  templateUrl: './sales-invoice-form.component.html',
  styleUrls: ['./sales-invoice-form.component.scss'],
})
export class SalesInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private service = inject(SalesInvoiceService);
  private store = inject(SalesInvoiceStore);

  form = this.fb.group({
    invoiceNumber: [''],
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    customerName: [''],
    buyerTin: [''],
    issueDate: [new Date().toISOString().split('T')[0], Validators.required],
    dueDate: [''],
    currency: ['MYR'],
    notes: [''],
    items: this.fb.array([]),
  });

  isEditMode = false;
  entityId: string | null = null;

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
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((invoice) => {
        this.form.patchValue({
          invoiceNumber: invoice.invoiceNumber,
          companyId: invoice.companyId,
          customerId: invoice.customerId,
          customerName: invoice.customerName,
          issueDate: invoice.issueDate,
          dueDate: invoice.dueDate,
          currency: invoice.currencyCode,
        });
        // Rebuild child FormArray from loaded items
        invoice.items?.forEach((item) => this.addItemRow(item));
      });
    }
  }

  addItemRow(item?: SalesInvoiceItemDto): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0],
      uom: [item?.uom ?? 'EA'],
    }));
  }

  recalculate(): void {
    const itemValues = this.items.controls.map(c => ({
      qty: c.get('quantity')?.value ?? 0,
      rate: c.get('unitPrice')?.value ?? 0,
      discountPercent: 0,
    }));

    this.calcResult = this.taxCalc.calculate(itemValues, []);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.recalculate();

    const dto: CreateSalesInvoiceDto = this.form.getRawValue() as any;

    if (this.isEditMode) {
      this.store.update({ id: this.entityId!, input: dto });
    } else {
      this.store.create(dto);
    }
    this.router.navigate(['/sales/invoices']);
  }

  cancel(): void {
    this.router.navigate(['/sales/invoices']);
  }
}
