import { Component, inject, OnInit, signal } from '@angular/core';
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
  private salesOrderService = inject(SalesOrderService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);
  warehouses = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  salesOrders = signal<any[]>([]);
  isLoadingSoItems = signal(false);

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
      error: () => {},
    });
  }

  cancel(): void {
    this.router.navigate(['/purchasing/material-requests']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
