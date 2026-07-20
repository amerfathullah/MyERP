import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-stock-entry-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './stock-entry-form.component.html',
  styleUrls: ['./stock-entry-form.component.scss'],
})
export class StockEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(StockEntryService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  warehouses = signal<any[]>([]);
  companies = signal<any[]>([]);
  linkedWorkOrderId: string | null = null;
  isLoadingBOM = false;
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: [''],
    entryType: ['Receipt', Validators.required],
    entryDate: [new Date(), Validators.required],
    sourceWarehouse: [''],
    targetWarehouse: ['', Validators.required],
    remarks: [''],
    items: this.fb.array([]),
  });
  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });
    }

    // Load warehouses and companies for dropdown selectors
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(se => {
        this.form.patchValue({
          companyId: se.companyId,
          entryType: se.entryType?.toString() ?? 'Receipt',
          entryDate: se.postingDate ? new Date(se.postingDate) : new Date(),
          remarks: se.notes ?? '',
        });
        (se.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId, Validators.required],
            itemName: [item.description ?? item.itemName ?? '', Validators.required],
            qty: [item.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
            uom: ['Unit'],
          }));
          // Set warehouse from first item if available
          if (item.sourceWarehouseId && !this.form.get('sourceWarehouse')?.value)
            this.form.patchValue({ sourceWarehouse: item.sourceWarehouseId });
          if (item.targetWarehouseId && !this.form.get('targetWarehouse')?.value)
            this.form.patchValue({ targetWarehouse: item.targetWarehouseId });
        });
      });
      return; // Skip query-param processing for edit mode
    }

    const params = this.route.snapshot.queryParams;
    if (params['purpose']) {
      const purposeMap: Record<string, string> = {
        'MaterialTransferForManufacture': 'Transfer',
        'Manufacture': 'Manufacture',
        'MaterialIssue': 'Issue',
        'MaterialTransfer': 'Transfer',
      };
      this.form.patchValue({ entryType: purposeMap[params['purpose']] ?? params['purpose'] });
    }
    if (params['workOrderId']) {
      this.linkedWorkOrderId = params['workOrderId'];
      this.form.patchValue({ remarks: `Against Work Order: ${params['workOrderId'].substring(0, 8)}...` });
      this.loadBomItems(params['workOrderId']);
    }
    if (params['sourceWarehouse']) {
      this.form.patchValue({ sourceWarehouse: params['sourceWarehouse'] });
    }
    if (params['targetWarehouse']) {
      this.form.patchValue({ targetWarehouse: params['targetWarehouse'] });
    }
  }

  loadBomItems(workOrderId: string): void {
    this.isLoadingBOM = true;
    const produceQty = 1; // Default to 1 unit — user can adjust
    this.service.getManufactureItems(workOrderId, produceQty).subscribe({
      next: (result) => {
        this.isLoadingBOM = false;
        if (result.sourceWarehouseId) {
          this.form.patchValue({ sourceWarehouse: result.sourceWarehouseId });
        }
        if (result.fgWarehouseId) {
          this.form.patchValue({ targetWarehouse: result.fgWarehouseId });
        }
        // Clear and populate items from BOM
        this.items.clear();
        for (const item of result.items ?? []) {
          this.items.push(this.fb.group({
            itemId: [item.itemId, Validators.required],
            itemName: [item.itemName, Validators.required],
            qty: [item.requiredQty, [Validators.required, Validators.min(0.01)]],
            uom: ['Unit'],
          }));
        }
        this.toaster.info(`Loaded ${result.items?.length ?? 0} items from BOM`);
      },
      error: () => {
        this.isLoadingBOM = false;
        this.toaster.warn('Could not load BOM items — add manually');
      },
    });
  }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      qty: [1, [Validators.required, Validators.min(0.01)]],
      uom: ['Unit'],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue() as any;
    // Map item 'qty' field to 'quantity' as expected by CreateStockEntryItemDto
    const dto = {
      ...raw,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        quantity: item.quantity ?? item.qty ?? 0,
        sourceWarehouseId: item.sourceWarehouseId || null,
        targetWarehouseId: item.targetWarehouseId || null,
      })),
    };
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => { this.toaster.success('Stock Entry updated'); this.router.navigate(['/inventory/stock-entries', this.entityId]); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Update failed'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => { this.toaster.success('Stock Entry created'); this.router.navigate(['/inventory/stock-entries']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/inventory/stock-entries']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
