import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemStore } from '../store/item.store';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-item-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule, RouterModule],
  templateUrl: './item-form.component.html',
  styleUrls: ['./item-form.component.scss'],
})
export class ItemFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private store = inject(ItemStore);
  private service = inject(ItemService);

  stockLevels = signal<any[]>([]);
  companies = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    itemCode: ['', [Validators.required, Validators.maxLength(50)]],
    itemName: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    itemType: [0, Validators.required],
    itemGroup: [''],
    uom: ['Unit'],
    standardSellingPrice: [0],
    standardBuyingPrice: [0],
    maintainStock: [true],
    isActive: [true],
    reorderLevel: [0],
    reorderQty: [0],
    safetyStock: [0],
    minOrderQty: [0],
    inspectionRequiredBeforePurchase: [false],
    inspectionRequiredBeforeDelivery: [false],
  });

  isEditMode = false;
  entityId: string | null = null;

  itemTypes = [
    { value: 0, label: 'Goods' },
    { value: 1, label: 'Service' },
    { value: 2, label: 'Fixed Asset' }];

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.http.get<any>('/api/app/company', { params: { skipCount: '0', maxResultCount: '100', sorting: '' } })
      .subscribe(res => this.companies.set(res.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((item) => {
        this.form.patchValue(item as any);
        // Load stock levels for this item
        this.http.get<any[]>(`/api/app/stock-balance/item-stock`, { params: { itemId: this.entityId! } })
          .subscribe(levels => this.stockLevels.set(levels ?? []));
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue() as any;

    if (this.isEditMode) {
      this.service.update(this.entityId!, value).subscribe({
        next: () => this.router.navigate(['/inventory/items']),
        error: () => {},
      });
    } else {
      this.service.create(value).subscribe({
        next: () => this.router.navigate(['/inventory/items']),
        error: () => {},
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}