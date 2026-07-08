import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CompanyService } from '../../proxy/core/company.service';
import { PurchaseOrderStore } from '../store/purchase-order.store';
import type { SupplierDto } from '../../proxy/purchasing/models';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-purchase-order-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatDatepickerModule, MatNativeDateModule, MatButtonModule, MatIconModule, MatTableModule,
  ],
  templateUrl: './purchase-order-form.component.html',
  styleUrls: ['./purchase-order-form.component.scss'],
})
export class PurchaseOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(PurchaseOrderStore);
  private service = inject(PurchaseOrderService);
  private supplierService = inject(SupplierService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  suppliers = signal<SupplierDto[]>([]);
  isEditMode = false;
  entityId: string | null = null;
  itemColumns = ['description', 'quantity', 'unitPrice', 'taxAmount', 'lineTotal', 'actions'];

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    orderDate: [new Date().toISOString().split('T')[0], Validators.required],
    expectedDeliveryDate: [''],
    notes: [''],
    items: this.fb.array([], Validators.minLength(1)),
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' })
      .subscribe(r => this.suppliers.set(r.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(po => {
        this.form.patchValue({
          companyId: po.companyId,
          supplierId: po.supplierId,
          orderDate: po.orderDate,
          expectedDeliveryDate: po.expectedDeliveryDate ?? '',
          notes: '',
        });
        po.items?.forEach(item => this.addItemRow(item));
      });
    } else {
      this.addItemRow();
    }
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0, Validators.min(0)],
      uom: [item?.uom ?? 'Unit'],
    }));
  }

  removeItemRow(index: number): void {
    this.items.removeAt(index);
  }

  getLineTotal(row: FormGroup): number {
    const qty = row.get('quantity')?.value ?? 0;
    const price = row.get('unitPrice')?.value ?? 0;
    const tax = row.get('taxAmount')?.value ?? 0;
    return qty * price + tax;
  }

  get netTotal(): number {
    return this.items.controls.reduce((sum, row) => {
      const g = row as FormGroup;
      return sum + (g.get('quantity')?.value ?? 0) * (g.get('unitPrice')?.value ?? 0);
    }, 0);
  }

  get taxTotal(): number {
    return this.items.controls.reduce((sum, row) => {
      return sum + ((row as FormGroup).get('taxAmount')?.value ?? 0);
    }, 0);
  }

  get grandTotal(): number { return this.netTotal + this.taxTotal; }

  save(): void {
    if (this.form.invalid || this.items.length === 0) {
      this.form.markAllAsTouched();
      if (this.items.length === 0) this.toaster.warn('Add at least one item');
      return;
    }
    const dto = this.form.getRawValue() as any;
    this.store.create(dto);
    this.router.navigate(['/purchasing/orders']);
  }

  cancel(): void {
    this.router.navigate(['/purchasing/orders']);
  }
}
