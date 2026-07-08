import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { Router, ActivatedRoute } from '@angular/router';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { DeliveryNoteStore } from '../store/delivery-note.store';

@Component({
  selector: 'app-delivery-note-form',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, ReactiveFormsModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatDatepickerModule,
  ],
  templateUrl: './delivery-note-form.component.html',
  styleUrls: ['./delivery-note-form.component.scss'],
})
export class DeliveryNoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(DeliveryNoteStore);
  private service = inject(DeliveryNoteService);

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
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

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
    this.store.create(value);
    this.router.navigate(['/sales/delivery-notes']);
  }
}
