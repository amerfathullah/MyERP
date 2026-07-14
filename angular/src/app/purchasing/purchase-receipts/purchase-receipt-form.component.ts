import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import type { CreatePurchaseReceiptDto } from '../../proxy/purchasing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-purchase-receipt-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './purchase-receipt-form.component.html',
  styleUrls: ['./purchase-receipt-form.component.scss'],
})
export class PurchaseReceiptFormComponent {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(PurchaseReceiptService);
  private toaster = inject(ToasterService);

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    warehouseId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    purchaseOrderId: [''],
    supplierDeliveryNote: [''],
    notes: [''],
    items: this.fb.array([]),
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      description: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      uom: ['EA'],
    }));
  }

  removeItem(i: number): void { this.items.removeAt(i); }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const dto = this.form.getRawValue() as unknown as CreatePurchaseReceiptDto;
    this.service.create(dto).subscribe({
      next: () => {
        this.toaster.success('Purchase Receipt created');
        this.router.navigate(['/purchasing/receipts']);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed to create'),
    });
  }

  cancel(): void {
    this.router.navigate(['/purchasing/receipts']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}