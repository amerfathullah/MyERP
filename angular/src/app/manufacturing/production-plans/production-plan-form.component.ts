import { Component, inject, OnInit, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { CreateProductionPlanDto } from '../../proxy/manufacturing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-production-plan-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './production-plan-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./production-plan-form.component.scss'],
})
export class ProductionPlanFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(ProductionPlanService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private companyService = inject(CompanyService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private mfgService = inject(ManufacturingService);

  companies = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  allBoms = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  loadingSoItems = signal(false);

  /** Returns active BOMs filtered by currently selected item in row (ERPNext d08131b6e8) */
  getFilteredBoms(itemId: string): any[] {
    const activeBoms = this.allBoms().filter((b: any) => b.isActive !== false);
    if (!itemId) return activeBoms;
    return activeBoms.filter((b: any) => b.itemId === itemId);
  }

  form = this.fb.group({
    companyId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    forWarehouseId: [''],
    subAssemblyWarehouseId: [''],
    rawMaterialGroupWarehouseId: [''],
    combineItems: [false],
    ignoreExistingOrderedQty: [false],
    considerMinimumOrderQty: [false],
    includeSafetyStock: [false],
    skipAvailableSubAssemblyItem: [false],
    notes: [''],
    items: this.fb.array([]),
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      bomId: ['', Validators.required],
      plannedQty: [1, [Validators.required, Validators.min(0.01)]],
      warehouseId: [''],
      plannedStartDate: [''],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  ngOnInit(): void {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => this.companies.set(res.items ?? []));

    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe(res => this.availableItems.set(res.items ?? []));

    this.mfgService.getBomList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe(res => this.allBoms.set(res.items ?? []));

    this.warehouseService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe(res => this.warehouses.set(res.items ?? []));

    // Pre-fill from Sales Order query param (SO → Production Plan workflow)
    const soId = this.route.snapshot.queryParamMap.get('salesOrderId');
    const qCompanyId = this.route.snapshot.queryParamMap.get('companyId');
    if (qCompanyId) this.form.patchValue({ companyId: qCompanyId });
    if (soId) {
      this.form.patchValue({ notes: `Production Plan for SO: ${soId}` });
      if (this.form.get('companyId')?.value) {
        this.loadOpenSalesOrderItems([soId]);
      }
    }
  }

  loadOpenSalesOrderItems(salesOrderIds?: string[]): void {
    const companyId = this.form.get('companyId')?.value;
    if (!companyId) {
      this.toaster.warn('::SelectCompanyFirst');
      return;
    }
    this.loadingSoItems.set(true);
    this.service.getOpenSalesOrderItems(companyId, salesOrderIds).subscribe({
      next: (soItems) => {
        this.loadingSoItems.set(false);
        const candidates = (soItems ?? []).filter(i => (i.unplannedQty ?? 0) > 0);
        if (candidates.length === 0) {
          this.toaster.info('::NoOpenSalesOrderItems');
          return;
        }
        const existingKeys = new Set(
          this.items.controls.map(c => `${c.get('itemId')?.value}:${c.get('bomId')?.value}`)
        );
        for (const soItem of candidates) {
          const itemId = soItem.itemId ?? '';
          const bomId = soItem.bomId || this.getFilteredBoms(itemId)[0]?.id || '';
          const key = `${itemId}:${bomId}`;
          if (!itemId || existingKeys.has(key)) continue;
          existingKeys.add(key);
          this.items.push(this.fb.group({
            itemId: [itemId, Validators.required],
            itemName: [soItem.itemName || soItem.itemCode || '', Validators.required],
            bomId: [bomId, Validators.required],
            plannedQty: [soItem.unplannedQty ?? soItem.orderedQty ?? 1, [Validators.required, Validators.min(0.01)]],
            warehouseId: [soItem.warehouseId || ''],
            plannedStartDate: [''],
          }));
        }
        this.form.markAsDirty();
      },
      error: (err) => {
        this.loadingSoItems.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToLoad');
      },
    });
  }

  onItemSelected(index: number): void {
    const row = this.items.at(index);
    const itemId = row.get('itemId')?.value;
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      row.patchValue({ itemName: item.itemName || item.itemCode });
    }
    // Auto-select default BOM if only one matches
    const boms = this.getFilteredBoms(itemId);
    if (boms.length === 1) {
      row.patchValue({ bomId: boms[0].id });
    } else {
      row.patchValue({ bomId: '' });
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const val = this.form.getRawValue();
    const dto: CreateProductionPlanDto = {
      companyId: val.companyId!,
      postingDate: val.postingDate ?? undefined,
      forWarehouseId: val.forWarehouseId || undefined,
      subAssemblyWarehouseId: val.subAssemblyWarehouseId || undefined,
      rawMaterialGroupWarehouseId: val.rawMaterialGroupWarehouseId || undefined,
      combineItems: val.combineItems ?? false,
      ignoreExistingOrderedQty: val.ignoreExistingOrderedQty ?? false,
      considerMinimumOrderQty: val.considerMinimumOrderQty ?? false,
      includeSafetyStock: val.includeSafetyStock ?? false,
      skipAvailableSubAssemblyItem: val.skipAvailableSubAssemblyItem ?? false,
      notes: val.notes ?? undefined,
      items: (val.items ?? []).map((i: any) => ({
        itemId: i.itemId,
        itemName: i.itemName,
        bomId: i.bomId,
        plannedQty: i.plannedQty,
        warehouseId: i.warehouseId || undefined,
        plannedStartDate: i.plannedStartDate || undefined,
      })),
    };

    this.service.create(dto).subscribe({
      next: (created) => {
        this.form.markAsPristine();
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/manufacturing/production-plans', created.id]);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
