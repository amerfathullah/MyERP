import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { ItemService } from '../../proxy/inventory/item.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { CreateProductionPlanDto } from '../../proxy/manufacturing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-production-plan-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, RouterModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './production-plan-form.component.html',
  styleUrls: ['./production-plan-form.component.scss'],
})
export class ProductionPlanFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(ProductionPlanService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private companyService = inject(CompanyService);
  private itemService = inject(ItemService);
  private mfgService = inject(ManufacturingService);

  companies = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  allBoms = signal<any[]>([]);

  /** Returns BOMs filtered by currently selected item in row */
  getFilteredBoms(itemId: string): any[] {
    if (!itemId) return this.allBoms();
    return this.allBoms().filter((b: any) => b.itemId === itemId);
  }

  form = this.fb.group({
    companyId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
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
        this.toaster.success('Production Plan created');
        this.router.navigate(['/manufacturing/production-plans', created.id]);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
