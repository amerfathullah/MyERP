import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MaterialRequestStore } from '../store/material-request.store';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { CompanyService } from '../../proxy/core/company.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemDetailsService } from '../../proxy/inventory/item-details.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-material-request-form',
  standalone: true,
  imports: [AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './material-request-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
  private itemDetailsService = inject(ItemDetailsService, { optional: true });
  private salesOrderService = inject(SalesOrderService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);
  warehouses = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  salesOrders = signal<any[]>([]);
  isLoadingSoItems = signal(false);
  /** Per-row stock availability from ItemDetailsService (0 when MR Transfer source warehouse is hidden/unpermitted per PR #67502). */
  rowActualQty = signal<Record<number, number | null>>({});

  get isMaterialTransfer(): boolean {
    return Number(this.form?.get('requestType')?.value) === 1;
  }

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

    this.form.get('requestType')?.valueChanges.subscribe((type) => {
      if (Number(type) !== 1) {
        this.form.get('sourceWarehouseId')?.setValue('', { emitEvent: false });
      }
      this.refreshAllRowsStock();
    });
    this.form.get('sourceWarehouseId')?.valueChanges.subscribe(() => this.refreshAllRowsStock());

    const cid = this.companyContext.currentCompanyId();
    if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' })
      .subscribe((res) => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' })
      .subscribe((res) => this.availableItems.set(res.items ?? []));

    // Load active SOs for "Get Items from SO" feature
    this.salesOrderService.getList({ skipCount: 0, maxResultCount: 100, sorting: 'orderDate desc', status: 'ToDeliverAndBill' } as any)
      .subscribe({ next: res => this.salesOrders.set(res.items ?? []), error: () => {} });
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
    this.fetchRowStock(index);
  }

  /**
   * Fetches stock availability via ItemDetailsService.
   * Per ERPNext PR #67502 (commits f7bff6daab, 67fe2bf517): Material Transfer passes fromWarehouseId
   * with isMaterialRequest/isMaterialTransfer so unpermitted source warehouses return 0 without failing.
   */
  fetchRowStock(index: number): void {
    if (!this.itemDetailsService) return;
    const row = this.items.at(index) as FormGroup | undefined;
    const itemId = row?.get('itemId')?.value;
    if (!itemId) {
      this.rowActualQty.update((curr) => ({ ...curr, [index]: null }));
      return;
    }
    const companyId = this.form.get('companyId')?.value || null;
    const warehouseId = row?.get('warehouseId')?.value || this.form.get('targetWarehouseId')?.value || null;
    const fromWarehouseId = this.isMaterialTransfer ? (this.form.get('sourceWarehouseId')?.value || null) : null;
    this.itemDetailsService.getItemDetails({
      itemId,
      companyId,
      transactionType: 'Buying',
      warehouseId,
      fromWarehouseId,
      isMaterialRequest: true,
      isMaterialTransfer: this.isMaterialTransfer,
    }).subscribe({
      next: (details) => {
        this.rowActualQty.update((curr) => ({ ...curr, [index]: details.actualQty ?? 0 }));
      },
      error: () => {
        this.rowActualQty.update((curr) => ({ ...curr, [index]: null }));
      },
    });
  }

  private refreshAllRowsStock(): void {
    for (let i = 0; i < this.items.length; i++) {
      if (this.items.at(i).get('itemId')?.value) {
        this.fetchRowStock(i);
      }
    }
  }

  getItemsFromSalesOrder(soId: string): void {
    if (!soId) return;
    this.isLoadingSoItems.set(true);
    this.salesOrderService.get(soId).subscribe({
      next: (so: any) => {
        const pendingItems = (so.items ?? []).filter((item: any) => {
          const pending = (item.quantity ?? 0) - (item.deliveredQty ?? 0);
          return pending > 0;
        });
        if (!pendingItems.length) {
          this.toaster.info(this.l.instant('::AllItemsAlreadyDelivered'));
          this.isLoadingSoItems.set(false);
          return;
        }
        // Clear existing rows and populate from SO
        while (this.items.length) this.items.removeAt(0);
        for (const item of pendingItems) {
          const pendingQty = (item.quantity ?? 0) - (item.deliveredQty ?? 0);
          this.items.push(this.fb.group({
            itemId: [item.itemId, Validators.required],
            itemName: [item.description || item.itemName || '', Validators.required],
            quantity: [pendingQty, [Validators.required, Validators.min(0.01)]],
            uom: [item.uom || 'Unit'],
            warehouseId: [item.warehouseId || ''],
          }));
        }
        this.toaster.success(this.l.instant('::ItemsLoadedFromSO', String(pendingItems.length)));
        this.isLoadingSoItems.set(false);
      },
      error: () => { this.isLoadingSoItems.set(false); },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue() as any;
    // Convert empty strings to null for nullable Guid fields (backend expects null, not '')
    const dto = {
      ...raw,
      sourceWarehouseId: raw.sourceWarehouseId || null,
      targetWarehouseId: raw.targetWarehouseId || null,
      requiredByDate: raw.requiredByDate || null,
      items: (raw.items ?? []).map((item: any) => ({
        ...item,
        warehouseId: item.warehouseId || null,
      })),
    };
    this.service.create(dto).subscribe({
      next: () => { this.form.markAsPristine(); this.router.navigate(['/purchasing/material-requests']); },
      error: (err) => { this.toaster.error(err?.error?.error?.message || '::FailedToSaveMaterialRequest'); },
    });
  }

  cancel(): void {
    this.router.navigate(['/purchasing/material-requests']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
