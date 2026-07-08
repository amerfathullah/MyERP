import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';
import { InvoiceItemGridComponent } from '../../sales/sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { PurchaseInvoiceStore } from '../store/purchase-invoice.store';
import type { CreatePurchaseInvoiceDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-purchase-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    MatCardModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSelectModule,
    InvoiceItemGridComponent,
  ],
  templateUrl: './purchase-invoice-form.component.html',
  styleUrls: ['./purchase-invoice-form.component.scss'],
})
export class PurchaseInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private service = inject(PurchaseInvoiceService);
  private store = inject(PurchaseInvoiceStore);

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    issueDate: [new Date().toISOString().split('T')[0], Validators.required],
    dueDate: [''],
    supplierInvoiceNumber: [''],
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
          companyId: invoice.companyId,
          supplierId: invoice.supplierId,
          issueDate: invoice.issueDate,
          dueDate: invoice.dueDate,
          supplierInvoiceNumber: invoice.supplierInvoiceNumber,
        });
        invoice.items?.forEach((item: any) => this.addItemRow(item));
      });
    }
  }

  addItemRow(item?: any): void {
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
    const dto: CreatePurchaseInvoiceDto = this.form.getRawValue() as any;
    this.store.create(dto);
    this.router.navigate(['/purchasing/invoices']);
  }

  cancel(): void {
    this.router.navigate(['/purchasing/invoices']);
  }
}
