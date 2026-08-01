import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { PickListService } from '../../proxy/inventory/pick-list.service';
import type { SalesOrderDto } from '../../proxy/sales/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { StockAvailabilityComponent } from '../../shared/components/stock-availability/stock-availability.component';
import { BatchExpiryWarningComponent } from '../../shared/components/batch-expiry-warning/batch-expiry-warning.component';

@Component({
  selector: 'app-delivery-note-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, StockAvailabilityComponent, BatchExpiryWarningComponent, CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule],
  templateUrl: './delivery-note-form.component.html',
  styleUrls: ['./delivery-note-form.component.scss'],
})
export class DeliveryNoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(DeliveryNoteService);
  private customerService = inject(CustomerService);
  private warehouseService = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private soService = inject(SalesOrderService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  private pickListService = inject(PickListService);

  customers = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  companies = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  availableSOs = signal<SalesOrderDto[]>([]);
  availablePickLists = signal<any[]>([]);
  isLoadingSOItems = signal(false);
  isLoadingPickListItems = signal(false);

  form = this.fb.group({
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    salesOrderId: [''],
    warehouseId: ['', Validators.required],
    isReturn: [false],
    returnAgainstId: [''],
    items: this.fb.array([]),
  });

  isEditMode = false;
  isReturn = false;
  entityId: string | null = null;

  get items(): FormArray { return this.form.get('items') as FormArray; }

  ngOnInit(): void {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.customers.set(res.items ?? [])
    );
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup))
    );
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? [])
    );
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? [])
    );
    this.loadPickLists();
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    // Auto-set companyId from company context for new documents
    if (!this.isEditMode && !this.form?.get?.('companyId')?.value) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }

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

    // Handle return creation from detail page "Create Return" action
    const returnAgainst = this.route.snapshot.queryParams['returnAgainst'];
    if (returnAgainst && !this.isEditMode) {
      this.isReturn = true;
      this.form.patchValue({ isReturn: true, returnAgainstId: returnAgainst });
      this.service.get(returnAgainst).subscribe(original => {
        this.form.patchValue({
          companyId: original.companyId,
          customerId: original.customerId,
          warehouseId: original.warehouseId,
          salesOrderId: original.salesOrderId ?? '',
        });
        // Add items with negative quantities for return
        original.items?.forEach((item: any) => {
          this.addItemRow({
            ...item,
            quantity: -(Math.abs(item.quantity ?? 0)),
          });
        });
      });
    }
  }

  addItemRow(item?: any): void {
    this.items.push(this.fb.group({
      itemId: [item?.itemId ?? '', Validators.required],
      description: [item?.description ?? '', Validators.required],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(0.01)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      taxAmount: [item?.taxAmount ?? 0],
      uom: [item?.uom ?? 'Unit'],
    }));
  }

  /** Load active Sales Orders for the selected customer */
  onCustomerChanged(): void {
    const customerId = this.form.get('customerId')?.value;
    if (!customerId) {
      this.availableSOs.set([]);
      return;
    }
    const companyId = this.form.get('companyId')?.value || this.companyContext.currentCompanyId();
    this.soService.getList({
      skipCount: 0, maxResultCount: 100, sorting: '',
      companyId: companyId || undefined,
      status: 'ToDeliverAndBill',
    } as any).subscribe({
      next: res => {
        const orders = (res.items ?? []).filter((so: any) => so.customerId === customerId);
        this.availableSOs.set(orders);
      },
      error: () => this.availableSOs.set([]),
    });
  }

  /** Auto-populate items from selected Sales Order */
  onSalesOrderChanged(): void {
    const soId = this.form.get('salesOrderId')?.value;
    if (!soId) return;

    this.isLoadingSOItems.set(true);
    this.soService.get(soId).subscribe({
      next: (so: SalesOrderDto) => {
        // Clear existing items
        while (this.items.length > 0) this.items.removeAt(0);

        // Auto-fill customer if not set
        if (!this.form.get('customerId')?.value && so.customerId) {
          this.form.patchValue({ customerId: so.customerId });
        }

        // Auto-fill warehouse from first pending item's warehouse (per ERPNext SO→DN mapper)
        let warehouseSet = false;

        // Add only items with pending delivery qty
        let loadedCount = 0;
        (so.items ?? []).forEach((item: any) => {
          const pendingQty = (item.quantity ?? 0) - (item.deliveredQty ?? 0);
          if (pendingQty > 0) {
            // Auto-set DN-level warehouse from first item's warehouse
            if (!warehouseSet && item.warehouseId && !this.form.get('warehouseId')?.value) {
              this.form.patchValue({ warehouseId: item.warehouseId });
              warehouseSet = true;
            }
            this.addItemRow({
              itemId: item.itemId,
              description: item.description,
              quantity: pendingQty,
              unitPrice: item.unitPrice ?? 0,
              uom: item.uom ?? 'Unit',
              warehouseId: item.warehouseId,
            });
            loadedCount++;
          }
        });

        if (loadedCount > 0) {
          this.toaster.success(this.l.instant('::ItemsLoadedFromSO', loadedCount.toString()));
        } else {
          this.toaster.info(this.l.instant('::AllItemsAlreadyDelivered'));
        }
        this.isLoadingSOItems.set(false);
      },
      error: () => {
        this.toaster.error(this.l.instant('::FailedToLoad'));
        this.isLoadingSOItems.set(false);
      },
    });
  }

  /** Load submitted Pick Lists for Delivery purpose. Per ERPNext: Pick List→DN is primary fulfillment workflow. */
  loadPickLists(): void {
    const companyId = this.form.get('companyId')?.value || this.companyContext.currentCompanyId();
    const params: any = { skipCount: '0', maxResultCount: '50', status: 'Submitted' };
    if (companyId) params.companyId = companyId;
    this.pickListService.getList({ skipCount: 0, maxResultCount: 50, sorting: '' } as any).subscribe({
      next: (res: any) => {
        const lists = (res.items ?? []).filter((pl: any) => pl.purpose === 'Delivery' || pl.purpose === 0);
        this.availablePickLists.set(lists);
      },
      error: () => this.availablePickLists.set([]),
    });
  }

  /** Auto-populate items from a submitted Pick List. Per ERPNext: creates DN with picked items + quantities. */
  getItemsFromPickList(pickListId: string): void {
    if (!pickListId) return;
    this.isLoadingPickListItems.set(true);
    this.pickListService.get(pickListId).subscribe({
      next: (pickList: any) => {
        // Clear existing items
        while (this.items.length > 0) this.items.removeAt(0);

        // Auto-fill customer from pick list if available
        if (pickList.customerId && !this.form.get('customerId')?.value) {
          this.form.patchValue({ customerId: pickList.customerId });
          this.onCustomerChanged();
        }

        // Add only items with pending transfer qty (picked - already transferred)
        let loadedCount = 0;
        (pickList.items ?? []).forEach((item: any) => {
          const pendingQty = (item.pickedQty ?? item.quantity ?? 0) - (item.transferredQty ?? 0);
          if (pendingQty > 0) {
            this.addItemRow({
              itemId: item.itemId,
              description: item.itemName || item.description || '',
              quantity: pendingQty,
              unitPrice: item.rate ?? 0,
              uom: item.uom ?? 'Unit',
            });
            loadedCount++;
          }
        });

        if (loadedCount > 0) {
          this.toaster.success(this.l.instant('::ItemsLoadedFromPickList', loadedCount.toString()));
        } else {
          this.toaster.info(this.l.instant('::AllItemsAlreadyTransferred'));
        }
        this.isLoadingPickListItems.set(false);
      },
      error: () => {
        this.toaster.error(this.l.instant('::FailedToLoad'));
        this.isLoadingPickListItems.set(false);
      },
    });
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue() as any;
    // Convert empty strings to null for nullable Guid fields
    const value = {
      ...raw,
      salesOrderId: raw.salesOrderId || null,
      returnAgainstId: raw.returnAgainstId || null,
    };
    if (this.isEditMode) {
      this.service.update(this.entityId!, value).subscribe({
        next: () => this.router.navigate(['/sales/delivery-notes', this.entityId]),
        error: () => {},
      });
    } else {
      this.service.create(value).subscribe({
        next: () => this.router.navigate(['/sales/delivery-notes']),
        error: () => {},
      });
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }

  getItemIds(): string[] {
    return this.items.controls
      .map(c => c.get('itemId')?.value as string)
      .filter(id => !!id);
  }
}
