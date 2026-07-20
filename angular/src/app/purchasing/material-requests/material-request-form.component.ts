import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { MaterialRequestStore } from '../store/material-request.store';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { CompanyService } from '../../proxy/core/company.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-material-request-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './material-request-form.component.html',
  styleUrls: ['./material-request-form.component.scss'],
})
export class MaterialRequestFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private store = inject(MaterialRequestStore);
  private service = inject(MaterialRequestService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);
  warehouses = signal<any[]>([]);
  availableItems = signal<any[]>([]);

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      requestType: [0, Validators.required],
      requestDate: [new Date().toISOString().split('T')[0], Validators.required],
      requiredByDate: [''],
      sourceWarehouseId: [''],
      targetWarehouseId: [''],
      notes: [''],
      items: this.fb.array([]),
    });
    this.addItemRow();

    const cid = this.companyContext.currentCompanyId();
    if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe((res) => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe((res) => this.availableItems.set(res.items ?? []));
  }

  addItemRow(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      uom: ['Unit'],
      warehouseId: [''],
    }));
  }

  removeItemRow(index: number): void {
    this.items.removeAt(index);
  }

  onItemSelected(index: number, itemId: string): void {
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      const row = this.items.at(index) as FormGroup;
      row.patchValue({ itemName: item.itemName || item.itemCode, uom: item.uom || 'Unit' });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.service.create(this.form.getRawValue() as any).subscribe({
      next: () => this.router.navigate(['/purchasing/material-requests']),
      error: () => {},
    });
  }

  cancel(): void {
    this.router.navigate(['/purchasing/material-requests']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
