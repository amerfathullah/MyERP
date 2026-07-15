import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { DeliveryNoteStore } from '../store/delivery-note.store';
import { CustomerService } from '../../proxy/sales/customer.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-delivery-note-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule],
  templateUrl: './delivery-note-form.component.html',
  styleUrls: ['./delivery-note-form.component.scss'],
})
export class DeliveryNoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(DeliveryNoteStore);
  private service = inject(DeliveryNoteService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);

  customers = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    salesOrderId: [''],
    warehouseId: [''],
    items: this.fb.array([]),
  });

  isEditMode = false;
  entityId: string | null = null;

  get items(): FormArray { return this.form.get('items') as FormArray; }

  ngOnInit(): void {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.customers.set(res.items ?? [])
    );
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Auto-set companyId from company context for new documents
    if (!this.isEditMode && !this.form?.get?.('companyId')?.value) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((dn) => {
        this.form.patchValue({
          companyId: dn.companyId,
          customerId: dn.customerId,
          postingDate: dn.postingDate,
          salesOrderId: dn.salesOrderId,
          warehouseId: dn.warehouseId,
        });
        dn.items?.forEach((item: any) => this.addItemRow(item));
      });
    }
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      uom: [item?.uom ?? 'Unit'],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue() as any;
    this.service.create(value).subscribe({
      next: () => this.router.navigate(['/sales/delivery-notes']),
      error: () => { /* handled by global error interceptor */ },
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
