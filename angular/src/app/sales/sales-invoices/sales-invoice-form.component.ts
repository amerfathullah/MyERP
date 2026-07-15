import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { InvoiceItemGridComponent } from './components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SalesInvoiceStore } from '../store/sales-invoice.store';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CreateSalesInvoiceDto, SalesInvoiceItemDto } from '../../proxy/sales/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-sales-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    InvoiceItemGridComponent,
    AutoValidationDirective],
  templateUrl: './sales-invoice-form.component.html',
  styleUrls: ['./sales-invoice-form.component.scss'],
})
export class SalesInvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private taxCalc = inject(TaxCalculationService);
  private service = inject(SalesInvoiceService);
  private customerService = inject(CustomerService);
  private store = inject(SalesInvoiceStore);

  customers = signal<any[]>([]);

  private companyContext = inject(CompanyContextService);

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
    isReturn: [false],
    returnAgainstId: [null as string | null],
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

    // Auto-set companyId from context for new documents
    if (!this.isEditMode) {
      const companyId = this.companyContext.currentCompanyId();
      if (companyId) this.form.patchValue({ companyId });
    }

    // Load customer list for dropdown
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe(res => this.customers.set(res.items ?? []));

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
    } else {
      // Check for duplicate-from param
      const duplicateFrom = this.route.snapshot.queryParams['duplicateFrom'];
      const returnAgainst = this.route.snapshot.queryParams['returnAgainst'];
      if (duplicateFrom) {
        this.service.get(duplicateFrom).subscribe((source) => {
          this.form.patchValue({
            companyId: source.companyId,
            customerId: source.customerId,
            customerName: source.customerName,
            issueDate: new Date().toISOString().split('T')[0],
            currency: source.currencyCode,
          });
          source.items?.forEach((item) => this.addItemRow(item));
          this.recalculate();
        });
      } else if (returnAgainst) {
        this.service.get(returnAgainst).subscribe((source) => {
          this.form.patchValue({
            companyId: source.companyId,
            customerId: source.customerId,
            customerName: source.customerName,
            issueDate: new Date().toISOString().split('T')[0],
            currency: source.currencyCode,
            notes: `Credit Note against ${source.invoiceNumber}`,
            isReturn: true,
            returnAgainstId: returnAgainst,
          });
          // Return items have negative quantities
          source.items?.forEach((item) => {
            if (item) {
              this.addItemRow({ ...item, quantity: -(item.quantity ?? 0) } as any);
            }
          });
          this.recalculate();
        });
      }
    }
  }

  addItemRow(item?: SalesInvoiceItemDto): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required]],
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

  /** Used by unsaved-changes route guard */
  hasUnsavedChanges(): boolean {
    return this.form.dirty;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.recalculate();

    const dto: CreateSalesInvoiceDto = this.form.getRawValue() as any;

    this.service.create(dto).subscribe({
      next: () => this.router.navigate(['/sales/invoices']),
      error: () => { /* handled by global error interceptor */ },
    });
  }

  cancel(): void {
    this.router.navigate(['/sales/invoices']);
  }
}
