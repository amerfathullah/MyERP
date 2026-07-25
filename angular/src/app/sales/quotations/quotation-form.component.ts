import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InvoiceItemGridComponent } from '../sales-invoices/components/invoice-item-grid.component';
import { TaxCalculationService, TaxCalculationResult } from '../../shared/services/tax-calculation.service';
import { QuotationService } from '../../proxy/sales/quotation.service';
import { CustomerService } from '../../proxy/sales/customer.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-quotation-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, PageModule, InvoiceItemGridComponent, LocalizationPipe],
  templateUrl: './quotation-form.component.html',
  styleUrls: ['./quotation-form.component.scss'],
})
export class QuotationFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private taxCalc = inject(TaxCalculationService);
  private quotationService = inject(QuotationService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);

  customers = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    quotationNumber: [''],
    companyId: ['', Validators.required],
    quotationDate: [new Date(), Validators.required],
    validUntil: [null as Date | null],
    customerId: ['', Validators.required],
    customerName: [''],
    items: this.fb.array([]),
  });

  calcResult: TaxCalculationResult = { netTotal: 0, taxLines: [], totalTax: 0, grandTotal: 0 };

  get items(): FormArray { return this.form.get('items') as FormArray; }

  ngOnInit(): void {
    const cid = this.companyContext.currentCompanyId();
    if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });

    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.customers.set(res.items ?? [])
    );
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? [])
    );

    // Detect edit mode from route param
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.entityId = id;
      this.quotationService.get(id).subscribe(q => {
        this.form.patchValue({
          quotationNumber: q.quotationNumber,
          companyId: q.companyId,
          quotationDate: q.issueDate as any,
          validUntil: q.validUntil as any,
          customerId: q.customerId,
          customerName: q.customerName,
        });
        // Load items into FormArray
        (q.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId],
            itemName: [item.description || item.itemName || ''],
            qty: [item.quantity ?? 0],
            rate: [item.unitPrice ?? 0],
            discountPercent: [0],
          }));
        });
        this.recalculate();
      });
    }
  }

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
    const raw = this.form.getRawValue() as any;
    // Map item fields from grid control names to DTO property names
    const dto = {
      ...raw,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
      })),
    };

    const request$ = this.isEditMode
      ? this.quotationService.update(this.entityId!, dto)
      : this.quotationService.create(dto);

    request$.subscribe({
      next: (result) => this.router.navigate(['/sales/quotations', result?.id ?? this.entityId]),
      error: () => { /* handled by global error interceptor */ },
    });
  }

  cancel(): void { this.router.navigate(['/sales/quotations']); }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
