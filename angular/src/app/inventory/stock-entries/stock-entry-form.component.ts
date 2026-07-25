import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { StockEntryType } from '../../proxy/inventory/stock-entry-type.enum';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ItemService } from '../../proxy/inventory/item.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { BarcodeScannerComponent, ScanEvent } from '../../shared/components/barcode-scanner/barcode-scanner.component';

// Map display labels → numeric enum values for API
const ENTRY_TYPE_TO_ENUM: Record<string, number> = {
  'MaterialReceipt': StockEntryType.MaterialReceipt,
  'MaterialIssue': StockEntryType.MaterialIssue,
  'MaterialTransfer': StockEntryType.MaterialTransfer,
  'MaterialTransferForManufacture': StockEntryType.MaterialTransferForManufacture,
  'Manufacture': StockEntryType.Manufacture,
  'Repack': StockEntryType.Repack,
  'SendToSubcontractor': StockEntryType.SendToSubcontractor,
  'MaterialConsumptionForManufacture': StockEntryType.MaterialConsumptionForManufacture,
  'Disassemble': StockEntryType.Disassemble,
  'SendToWarehouse': StockEntryType.SendToWarehouse,
  'ReceiveAtWarehouse': StockEntryType.ReceiveAtWarehouse,
  'SubcontractingDelivery': StockEntryType.SubcontractingDelivery,
  'SubcontractingReturn': StockEntryType.SubcontractingReturn,
  'Adjustment': StockEntryType.Adjustment,
};

// Reverse map: numeric enum → display label for edit mode
const ENUM_TO_ENTRY_TYPE: Record<number, string> = Object.entries(ENTRY_TYPE_TO_ENUM)
  .reduce((acc, [k, v]) => ({ ...acc, [v]: k }), {} as Record<number, string>);

@Component({
  selector: 'app-stock-entry-form',
  standalone: true,
  imports: [
    AutoValidationDirective, SaveShortcutDirective, BarcodeScannerComponent, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
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
  private itemService = inject(ItemService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  warehouses = signal<any[]>([]);
  companies = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  linkedWorkOrderId: string | null = null;
  isLoadingBOM = false;
  isEditMode = false;
  entityId: string | null = null;

  form = this.fb.group({
    companyId: [''],
    entryType: ['MaterialReceipt', Validators.required],
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

    // Load warehouses, companies, and items for dropdown selectors
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? []));
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe(
      res => this.availableItems.set(res.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(se => {
        this.form.patchValue({
          companyId: se.companyId,
          entryType: ENUM_TO_ENTRY_TYPE[se.entryType ?? 0] ?? 'MaterialReceipt',
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
        'MaterialTransferForManufacture': 'MaterialTransferForManufacture',
        'Manufacture': 'Manufacture',
        'MaterialIssue': 'MaterialIssue',
        'MaterialTransfer': 'MaterialTransfer',
        'MaterialReceipt': 'MaterialReceipt',
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

  onItemSelected(index: number, event: Event): void {
    const itemId = (event.target as HTMLSelectElement).value;
    const item = this.availableItems().find((i: any) => i.id === itemId);
    if (item) {
      this.items.at(index).patchValue({ itemName: item.itemName ?? item.itemCode });
    }
  }

  /**
   * Handle barcode scan results per ERPNext patterns:
   * - Warehouse scan: sets warehouse context (sticky, all subsequent items use it)
   * - Serial item: always creates new row
   * - Non-serial item: increments qty if same item exists, otherwise new row
   */
  onBarcodeScan(event: ScanEvent): void {
    const { result, warehouseContext } = event;

    if (!result.success) return;

    // Warehouse context scan — update source/target warehouse
    if (result.scanType === 4 && result.warehouseId) {
      // Set the source warehouse (for issue/transfer) on form if present
      if (this.form.get('sourceWarehouse')) {
        this.form.patchValue({ sourceWarehouse: result.warehouseId });
      }
      this.toaster.info(`Location set: ${result.warehouseName}`);
      return;
    }

    // Item/Serial/Batch scan — add or increment
    if (!result.itemId) return;

    // Per gotcha #127: serial items always get new rows
    if (result.action === 2 /* AddNewRow */ || result.hasSerialNo) {
      this.items.push(this.fb.group({
        itemId: [result.itemId, Validators.required],
        itemName: [result.itemName ?? result.itemCode ?? '', Validators.required],
        qty: [1, [Validators.required, Validators.min(0.01)]],
        uom: [result.uom ?? 'Unit'],
      }));
      return;
    }

    // Per gotcha #127: non-serial items increment qty on existing row
    const existingIndex = this.items.controls.findIndex(
      ctrl => ctrl.get('itemId')?.value === result.itemId
    );

    if (existingIndex >= 0) {
      const currentQty = this.items.at(existingIndex).get('qty')?.value ?? 0;
      this.items.at(existingIndex).patchValue({ qty: currentQty + 1 });
    } else {
      this.items.push(this.fb.group({
        itemId: [result.itemId, Validators.required],
        itemName: [result.itemName ?? result.itemCode ?? '', Validators.required],
        qty: [1, [Validators.required, Validators.min(0.01)]],
        uom: [result.uom ?? 'Unit'],
      }));
    }
  }

  save(): void {
    if (this.form.invalid) return;
    const raw = this.form.getRawValue() as any;
    // Convert entryType from display string to numeric enum
    const entryType = ENTRY_TYPE_TO_ENUM[raw.entryType] ?? 0;
    // Map item 'qty' field to 'quantity' as expected by CreateStockEntryItemDto
    const dto = {
      ...raw,
      entryType,
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
