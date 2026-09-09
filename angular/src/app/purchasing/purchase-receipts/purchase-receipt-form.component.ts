import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { CreatePurchaseReceiptDto, PurchaseOrderDto } from '../../proxy/purchasing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-purchase-receipt-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './purchase-receipt-form.component.html',
  styleUrls: ['./purchase-receipt-form.component.scss'],
})
export class PurchaseReceiptFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(PurchaseReceiptService);
  private toaster = inject(ToasterService);
  private supplierService = inject(SupplierService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);
  private poService = inject(PurchaseOrderService);
  private l = inject(LocalizationService);

  suppliers = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  availablePOs = signal<PurchaseOrderDto[]>([]);
  isLoadingPOItems = signal(false);
  isApplyingPutaway = signal(false);
  isEditMode = false;
  isReturn = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: ['', Validators.required],
    supplierId: ['', Validators.required],
    warehouseId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    purchaseOrderId: [''],
    supplierDeliveryNote: [''],
    isReturn: [false],
    returnAgainstId: [''],
    notes: [''],
    items: this.fb.array([]),
  });

  get items(): FormArray { return this.form.get('items') as FormArray; }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      description: ['', Validators.required],
      warehouseId: [''],
      quantity: [1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      uom: ['EA'],
    }));
  }

  removeItem(i: number): void { this.items.removeAt(i); }

  /** Load submitted POs for the selected supplier */
  onSupplierChanged(): void {
    const supplierId = this.form.get('supplierId')?.value;
    if (!supplierId) {
      this.availablePOs.set([]);
      return;
    }
    const companyId = this.form.get('companyId')?.value || this.companyContext.currentCompanyId();
    this.poService.getList({
      skipCount: 0, maxResultCount: 100, sorting: '',
      companyId: companyId || undefined,
      status: 'ToDeliverAndBill',
    } as any).subscribe({
      next: res => {
        // Filter client-side by supplierId (backend may not filter by supplier)
        const pos = (res.items ?? []).filter((po: any) => po.supplierId === supplierId);
        this.availablePOs.set(pos);
      },
      error: () => this.availablePOs.set([]),
    });
  }

  /** Auto-populate items from selected Purchase Order */
  onPurchaseOrderChanged(): void {
    const poId = this.form.get('purchaseOrderId')?.value;
    if (!poId) return;

    this.isLoadingPOItems.set(true);
    this.poService.get(poId).subscribe({
      next: (po: PurchaseOrderDto) => {
        // Clear existing items
        while (this.items.length > 0) this.items.removeAt(0);

        // Auto-fill supplier if not already set
        if (!this.form.get('supplierId')?.value && po.supplierId) {
          this.form.patchValue({ supplierId: po.supplierId });
        }

        // Add only items with pending receipt qty
        let loadedCount = 0;
        (po.items ?? []).forEach(item => {
          const pendingQty = (item.quantity ?? 0) - (item.receivedQty ?? 0);
          if (pendingQty > 0) {
            this.items.push(this.fb.group({
              itemId: [item.itemId ?? '', Validators.required],
              description: [item.description ?? '', Validators.required],
              warehouseId: [''],
              quantity: [pendingQty, [Validators.required, Validators.min(0.01)]],
              unitPrice: [item.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
              uom: [item.uom ?? 'EA'],
              purchaseOrderItemId: [item.id ?? null],
            }));
            loadedCount++;
          }
        });

        if (loadedCount > 0) {
          this.toaster.success(this.l.instant('::ItemsLoadedFromPO', loadedCount.toString()));
        } else {
          this.toaster.info(this.l.instant('::AllItemsAlreadyReceived'));
        }
        this.isLoadingPOItems.set(false);
      },
      error: () => {
        this.toaster.error(this.l.instant('::FailedToLoad'));
        this.isLoadingPOItems.set(false);
      },
    });
  }

  /**
   * ERPNext's "Apply Putaway Rule" on Purchase Receipt: asks the backend which warehouses should
   * receive each item given the configured rules' priority and remaining capacity, then rewrites
   * the item rows to match — one row per allocated warehouse.
   *
   * Quantities are summed per item before asking (rules are capacity-limited per item+warehouse,
   * so two rows of the same item must not each be allocated as if the other did not exist), then
   * the returned allocations are walked back across that item's original rows in order, so each
   * row keeps its own price, UOM and PO line while being split at the allocation boundaries.
   */
  applyPutawayRule(): void {
    const companyId = this.form.get('companyId')?.value || this.companyContext.currentCompanyId();
    if (!companyId) { this.toaster.error('::SelectCompanyFirst'); return; }
    if (this.isReturn) { this.toaster.info(this.l.instant('::PutawayNotApplicableToReturns')); return; }

    // Half-finished rows (no item picked yet, or zero qty) are carried through untouched rather
    // than dropped — rebuilding the array must not delete what the user is still typing.
    const rows = (this.items.getRawValue() as any[])
      .map(row => ({ ...row, allocatable: !!row.itemId && Number(row.quantity) > 0 }));
    if (!rows.some(row => row.allocatable)) { this.toaster.info(this.l.instant('::NoItemsAddedYet')); return; }

    const qtyByItem = new Map<string, number>();
    rows.filter(row => row.allocatable)
      .forEach(row => qtyByItem.set(row.itemId, (qtyByItem.get(row.itemId) ?? 0) + Number(row.quantity)));

    this.isApplyingPutaway.set(true);
    this.service.getPutawayAllocations(
      companyId,
      Array.from(qtyByItem, ([itemId, qty]) => ({ itemId, qty })),
    ).subscribe({
      next: (allocations: any[]) => {
        this.isApplyingPutaway.set(false);
        this.rebuildItemsFromAllocations(rows, allocations ?? []);
      },
      error: (err: any) => {
        this.isApplyingPutaway.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToLoad');
      },
    });
  }

  private rebuildItemsFromAllocations(rows: any[], allocations: any[]): void {
    // An allocation with no warehouse could not be placed by any rule; it stays on the receipt's
    // own warehouse (blank override) so the row is still saveable and visibly unallocated.
    const queueByItem = new Map<string, { warehouseId: string; qty: number }[]>();
    allocations.forEach(a => {
      const queue = queueByItem.get(a.itemId) ?? [];
      queue.push({ warehouseId: a.isUnallocated ? '' : a.warehouseId, qty: Number(a.qty) });
      queueByItem.set(a.itemId, queue);
    });

    if (queueByItem.size === 0) { this.toaster.info(this.l.instant('::NoPutawayRulesYet')); return; }

    const rebuilt: any[] = [];
    let splitCount = 0;
    rows.forEach(row => {
      const queue = row.allocatable ? queueByItem.get(row.itemId) : undefined;
      if (!queue || queue.length === 0) { rebuilt.push(row); return; }

      let pending = Number(row.quantity);
      let producedForRow = 0;
      while (pending > 0 && queue.length > 0) {
        const head = queue[0];
        const take = Math.min(pending, head.qty);
        rebuilt.push({ ...row, quantity: take, warehouseId: head.warehouseId });
        producedForRow++;
        pending -= take;
        head.qty -= take;
        if (head.qty <= 0) queue.shift();
      }
      // Anything left over (allocations ran short of the row) keeps the receipt warehouse.
      if (pending > 0) { rebuilt.push({ ...row, quantity: pending, warehouseId: '' }); producedForRow++; }
      if (producedForRow > 1) splitCount++;
    });

    while (this.items.length > 0) this.items.removeAt(0);
    rebuilt.forEach(row => this.items.push(this.fb.group({
      itemId: [row.itemId ?? '', Validators.required],
      description: [row.description ?? '', Validators.required],
      warehouseId: [row.warehouseId ?? ''],
      quantity: [row.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [row.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      uom: [row.uom ?? 'EA'],
      purchaseOrderItemId: [row.purchaseOrderItemId ?? null],
    })));
    this.form.markAsDirty();

    if (splitCount > 0) {
      this.toaster.success(this.l.instant('::PutawayRuleApplied'));
    } else {
      this.toaster.info(this.l.instant('::PutawayRuleAppliedNoSplit'));
    }
  }

  onItemSelected(index: number, event: Event): void {
    const itemId = (event.target as HTMLSelectElement).value;
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      this.items.at(index).patchValue({ description: item.itemName ?? item.itemCode });
    }
  }

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (!this.isEditMode) {
      const cid = this.companyContext.currentCompanyId();
      if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });
    }

    this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.suppliers.set(res.items ?? [])
    );
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup))
    );
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? [])
    );

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(pr => {
        this.form.patchValue({
          companyId: pr.companyId,
          supplierId: pr.supplierId,
          warehouseId: pr.warehouseId ?? '',
          postingDate: pr.postingDate,
          purchaseOrderId: pr.purchaseOrderId ?? '',
          supplierDeliveryNote: pr.supplierDeliveryNote ?? '',
          notes: '',
        });
        (pr.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId ?? '', Validators.required],
            description: [item.description ?? '', Validators.required],
            warehouseId: [item.warehouseId ?? ''],
            quantity: [item.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
            unitPrice: [item.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
            uom: [item.uom ?? 'EA'],
          }));
        });
      });
    }

    // Handle return creation from detail page "Create Return" action
    const returnAgainst = this.route.snapshot.queryParams['returnAgainst'];
    if (returnAgainst && !this.isEditMode) {
      this.isReturn = true;
      this.form.patchValue({ isReturn: true, returnAgainstId: returnAgainst });
      this.service.get(returnAgainst).subscribe(original => {
        this.form.patchValue({
          companyId: original.companyId,
          supplierId: original.supplierId,
          warehouseId: original.warehouseId ?? '',
          purchaseOrderId: original.purchaseOrderId ?? '',
        });
        (original.items ?? []).forEach((item: any) => {
          this.items.push(this.fb.group({
            itemId: [item.itemId ?? '', Validators.required],
            description: [item.description ?? '', Validators.required],
            warehouseId: [item.warehouseId ?? ''],
            quantity: [-(Math.abs(item.quantity ?? 0)), [Validators.required]],
            unitPrice: [item.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
            uom: [item.uom ?? 'EA'],
          }));
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue() as any;
    // Convert empty strings to null for nullable Guid fields
    const dto = {
      ...raw,
      purchaseOrderId: raw.purchaseOrderId || null,
      returnAgainstId: raw.returnAgainstId || null,
      items: (raw.items ?? []).map((item: any) => ({
        ...item,
        purchaseOrderItemId: item.purchaseOrderItemId || undefined,
        warehouseId: item.warehouseId || null,
      })),
    } as unknown as CreatePurchaseReceiptDto;
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => {
          this.form.markAsPristine();
          this.toaster.success('::SuccessfullyUpdated');
          this.router.navigate(['/purchasing/receipts', this.entityId]);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? '::FailedToUpdate'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => {
          this.form.markAsPristine();
          this.toaster.success('::SuccessfullyCreated');
          this.router.navigate(['/purchasing/receipts']);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? '::FailedToCreate'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/purchasing/receipts']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
