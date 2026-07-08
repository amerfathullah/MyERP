import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemStore } from '../store/item.store';

@Component({
  selector: 'app-item-form',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, ReactiveFormsModule, RouterModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatSlideToggleModule,
  ],
  templateUrl: './item-form.component.html',
  styleUrls: ['./item-form.component.scss'],
})
export class ItemFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private store = inject(ItemStore);
  private service = inject(ItemService);

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
  });

  isEditMode = false;
  entityId: string | null = null;

  itemTypes = [
    { value: 0, label: 'Goods' },
    { value: 1, label: 'Service' },
    { value: 2, label: 'Fixed Asset' },
  ];

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((item) => {
        this.form.patchValue(item as any);
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
      this.store.update({ id: this.entityId!, input: value });
    } else {
      this.store.create(value);
    }
    this.router.navigate(['/inventory/items']);
  }
}
