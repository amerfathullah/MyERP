import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import { StockEntryType } from '../../proxy/inventory/stock-entry-type.enum';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { StockBalanceService } from '../../proxy/inventory/stock-balance.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { BarcodeScannerComponent, ScanEvent } from '../../shared/components/barcode-scanner/barcode-scanner.component';
import { ItemPickerComponent } from '../../shared/components/item-picker/item-picker.component';
import type { ItemDto } from '../../proxy/inventory/models';

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
    AutoValidationDirective, SaveShortcutDirective, BarcodeScannerComponent, ItemPickerComponent, CommonModule, ReactiveFormsModule, FormsModule, PageModule, LocalizationPipe],
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
  private mfgService = inject(ManufacturingService);
  private stockBalanceService = inject(StockBalanceService);

  warehouses = signal<any[]>([]);
  companies = signal<any[]>([]);
  workOrders = signal<any[]>([]);
  linkedWorkOrderId: string | null = null;
  isLoadingBOM = false;
  isEditMode = false;
  entityId: string | null = null;
  showBomPicker = false;
  selectedWorkOrderId = '';
  selectedCompanyId = signal<string>('');

  // Per ERPNext PR #60189 (commit 5b1bade8a2): exclude current company's sample retention warehouse from source warehouse pickers
  sourceWarehouses = computed(() => {
    const cid = this.selectedCompanyId();
    const all = this.warehouses();
    if (!cid) return all;

    const company = this.companies().find(c => c.id === cid);
    if (company?.sampleRetentionWarehouseId) {
      return all.filter(w => w.id !== company.sampleRetentionWarehouseId);
    }
    return all;
  });

  // Stock availability per item (fetched on item selection)
  // Map: itemId → { actualQty, reservedQty, availableQty, projectedQty, warehouseName }
  itemStockInfo = signal<Record<string, { actualQty: number; reservedQty: number; availableQty: number; projectedQty: number }>>({});

  produceQty = signal<number>(1);

  form = this.fb.group({
    companyId: [''],
    entryType: ['MaterialReceipt', Validators.required],
    entryDate: [new Date(), Validators.required],
    sourceWarehouse: [''],
    targetWarehouse: [''],
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

    this.selectedCompanyId.set(this.form.get('companyId')?.value ?? '');
    this.form.get('companyId')?.valueChanges.subscribe(cid => {
      this.selectedCompanyId.set(cid ?? '');
    });

    // Load warehouses, companies, and items for dropdown selectors
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe(
      res => this.warehouses.set((res.items ?? []).filter((w: any) => !w.isGroup)));
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe(
      res => this.companies.set(res.items ?? []));
    // Load active work orders for BOM picker
    this.mfgService.getWorkOrderList({ maxResultCount: 100, skipCount: 0, sorting: '' } as any).subscribe({
      next: (res: any) => this.workOrders.set(res.items ?? []),
      error: () => {},
    });

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
    if (params['materialRequestId']) {
      this.loadMaterialRequestItems(params['materialRequestId']);
    }
    if (params['sourceWarehouse']) {
      this.form.patchValue({ sourceWarehouse: params['sourceWarehouse'] });
    }
    if (params['targetWarehouse']) {
      this.form.patchValue({ targetWarehouse: params['targetWarehouse'] });
    }
  }

  onProduceQtyChanged(qty: number): void {
    this.produceQty.set(qty > 0 ? qty : 1);
    if (this.linkedWorkOrderId) {
      this.loadBomItems(this.linkedWorkOrderId);
    }
  }

  loadBomItems(workOrderId: string): void {
    this.isLoadingBOM = true;
    const qty = this.produceQty();
    this.service.getManufactureItems(workOrderId, qty).subscribe({
      next: (result) => {
        this.isLoadingBOM = false;
        if (result.sourceWarehouseId) {
          this.form.patchValue({ sourceWarehouse: result.sourceWarehouseId });
        }
        if (result.fgWarehouseId) {
          this.form.patchValue({ targetWarehouse: result.fgWarehouseId });
        }
        // Clear and populate items from BOM (RM + FG)
        this.items.clear();
        for (const item of result.items ?? []) {
          this.items.push(this.fb.group({
            itemId: [item.itemId, Validators.required],
            itemName: [item.isRawMaterial ? item.itemName : `✓ ${item.itemName}`, Validators.required],
            qty: [item.requiredQty, [Validators.required, Validators.min(0.01)]],
            uom: ['Unit'],
            isFinishedItem: [!item.isRawMaterial],
            sourceWarehouseId: [item.sourceWarehouseId || ''],
            targetWarehouseId: [item.targetWarehouseId || ''],
          }));
        }
        const rmCount = (result.items ?? []).filter((i: any) => i.isRawMaterial).length;
        const fgCount = (result.items ?? []).filter((i: any) => !i.isRawMaterial).length;
        this.toaster.info(`Loaded ${rmCount} raw materials + ${fgCount} finished good(s) from BOM`);
      },
      error: () => {
        this.isLoadingBOM = false;
        this.toaster.warn('::CouldNotLoadBomItems');
      },
    });
  }

  getItemsFromBom(): void {
    if (!this.selectedWorkOrderId) return;
    this.linkedWorkOrderId = this.selectedWorkOrderId;
    this.showBomPicker = false;
    this.loadBomItems(this.selectedWorkOrderId);
  }

  loadMaterialRequestItems(materialRequestId: string): void {
    this.service.getItemsFromMaterialRequest(materialRequestId).subscribe({
      next: (result: any) => {
        if (result.suggestedPurpose) {
          this.form.patchValue({ entryType: result.suggestedPurpose });
        }
        if (result.sourceWarehouseId) {
          this.form.patchValue({ sourceWarehouse: result.sourceWarehouseId });
        }
        if (result.targetWarehouseId) {
          this.form.patchValue({ targetWarehouse: result.targetWarehouseId });
        }
        this.form.patchValue({ remarks: `Against Material Request: ${result.materialRequestNumber ?? materialRequestId.substring(0, 8)}` });

        // Populate items
        while (this.items.length > 0) this.items.removeAt(0);
        for (const item of result.items ?? []) {
          const qty = item.quantity ?? 0;
          const rate = item.basicRate ?? 0;
          this.items.push(this.fb.group({
            itemId: [item.itemId, Validators.required],
            itemName: [item.itemName ?? '', Validators.required],
            qty: [qty, [Validators.required, Validators.min(0.001)]],
            uom: [item.uom ?? 'Unit'],
            conversionFactor: [1],
            transferQty: [qty],
            basicRate: [rate],
            amount: [Math.round(qty * rate * 100) / 100],
            setBasicRateManually: [false],
            isFinishedItem: [false],
            sourceWarehouseId: [result.sourceWarehouseId ?? ''],
            targetWarehouseId: [item.warehouseId ?? result.targetWarehouseId ?? ''],
            materialRequestItemId: [item.materialRequestItemId ?? null],
          }));
        }
        this.toaster.success(`${result.items?.length ?? 0} items loaded from Material Request`);
      },
      error: () => {
        this.toaster.warn('::CouldNotLoadMaterialRequestItems');
      },
    });
  }

  /** Returns true when entry type consumes stock from source warehouse (needs availability check) */
  isStockOutType(): boolean {
    const type = this.form.get('entryType')?.value;
    return ['MaterialIssue', 'MaterialTransfer', 'MaterialTransferForManufacture',
      'SendToSubcontractor', 'MaterialConsumptionForManufacture', 'SendToWarehouse',
      'SubcontractingDelivery'].includes(type);
  }

  /**
   * Per ERPNext: Source Warehouse is shown for types that consume/move stock.
   * Hidden for: MaterialReceipt, ReceiveAtWarehouse, Adjustment
   */
  showSourceWarehouse(): boolean {
    const type = this.form.get('entryType')?.value;
    return !['MaterialReceipt', 'ReceiveAtWarehouse', 'Adjustment'].includes(type);
  }

  /**
   * Per ERPNext: Target Warehouse is shown for types that receive stock.
   * Hidden for: MaterialIssue, MaterialConsumptionForManufacture
   */
  showTargetWarehouse(): boolean {
    const type = this.form.get('entryType')?.value;
    return !['MaterialIssue', 'MaterialConsumptionForManufacture'].includes(type);
  }

  /** Returns available qty for item at given row index (from source warehouse), or null if not loaded */
  getItemAvailableQty(index: number): number | null {
    const itemId = this.items.at(index)?.get('itemId')?.value;
    if (!itemId) return null;
    const info = this.itemStockInfo();
    return info[itemId]?.availableQty ?? null;
  }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      qty: [1, [Validators.required, Validators.min(0.01)]],
      uom: ['Unit'],
      conversionFactor: [1],
      transferQty: [1],
      basicRate: [0],
      amount: [0],
      setBasicRateManually: [false],
      isFinishedItem: [false],
      sourceWarehouseId: [''],
      targetWarehouseId: [''],
      materialRequestItemId: [null],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  onItemSelected(index: number, item: ItemDto | null): void {
    if (item?.id) {
      const basicRate = item.standardBuyingPrice ?? 0;
      this.items.at(index).patchValue({
        itemName: item.itemName ?? item.itemCode,
        uom: item.uom ?? 'Unit',
        basicRate,
      });
      this.calculateRowAmount(index);
      this.fetchStockAndRateForItem(index, item.id);
    }
  }

  /**
   * Per ERPNext PR #60751 / commit 44b0443ef8:
   * Recalculates stock entry row amounts and transfer_qty when qty changes.
   * If source warehouse is set and rate not manually set, refetches basic rate.
   * Otherwise keeps entered rate and recalculates transfer_qty and amount.
   */
  onRowQtyOrConversionChanged(index: number): void {
    const row = this.items.at(index);
    if (!row) return;

    const qty = Number(row.get('qty')?.value ?? 0);
    const conv = Number(row.get('conversionFactor')?.value ?? 1) || 1;
    const transferQty = qty * conv;
    row.patchValue({ transferQty }, { emitEvent: false });

    const sourceWarehouse = row.get('sourceWarehouseId')?.value || this.form.get('sourceWarehouse')?.value;
    const itemId = row.get('itemId')?.value;
    const setBasicRateManually = !!row.get('setBasicRateManually')?.value;

    if (sourceWarehouse && itemId && !setBasicRateManually) {
      this.fetchStockAndRateForItem(index, itemId);
    } else {
      this.calculateRowAmount(index);
    }
  }

  onRateChanged(index: number): void {
    const row = this.items.at(index);
    if (!row) return;
    row.patchValue({ setBasicRateManually: true }, { emitEvent: false });
    this.calculateRowAmount(index);
  }

  calculateRowAmount(index: number): void {
    const row = this.items.at(index);
    if (!row) return;
    const qty = Number(row.get('qty')?.value ?? 0);
    const conv = Number(row.get('conversionFactor')?.value ?? 1) || 1;
    const rate = Number(row.get('basicRate')?.value ?? 0);
    const transferQty = qty * conv;
    const amount = transferQty * rate;
    row.patchValue({ transferQty, amount: Math.round(amount * 100) / 100 }, { emitEvent: false });
  }

  totalAmount(): number {
    return this.items.controls.reduce((sum, ctrl) => sum + (Number(ctrl.get('amount')?.value) || 0), 0);
  }

  /**
   * Fetches stock availability and valuation rate for an item across warehouses.
   * Per ERPNext PR #60751 & update_bin_details: updates availability and rate.
   */
  fetchStockAndRateForItem(index: number, itemId: string): void {
    if (!itemId) return;
    const row = this.items.at(index);
    const sourceWarehouse = row?.get('sourceWarehouseId')?.value || this.form.get('sourceWarehouse')?.value;
    this.stockBalanceService.getItemStock(itemId).subscribe({
      next: (result: any) => {
        if (result?.length > 0) {
          const matchingBin = sourceWarehouse ? result.find((b: any) => b.warehouseId === sourceWarehouse) || result[0] : result[0];
          const info = this.itemStockInfo();
          this.itemStockInfo.set({
            ...info,
            [itemId]: {
              actualQty: matchingBin.actualQty ?? 0,
              reservedQty: matchingBin.reservedQty ?? 0,
              availableQty: (matchingBin.actualQty ?? 0) - (matchingBin.reservedQty ?? 0),
              projectedQty: matchingBin.projectedQty ?? 0,
            }
          });

          if (row && !row.get('setBasicRateManually')?.value) {
            const rate = matchingBin.valuationRate ?? 0;
            row.patchValue({ basicRate: rate }, { emitEvent: false });
            this.calculateRowAmount(index);
          }
        }
      },
      error: () => {}
    });
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
    const dto: any = {
      ...raw,
      entryType,
      workOrderId: this.linkedWorkOrderId || undefined,
      fgCompletedQty: this.produceQty() > 0 ? this.produceQty() : undefined,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        quantity: item.qty ?? item.quantity ?? 0,
        valuationRate: item.basicRate ? Number(item.basicRate) : undefined,
        setBasicRateManually: !!item.setBasicRateManually,
        conversionFactor: item.conversionFactor ? Number(item.conversionFactor) : 1,
        stockUom: item.uom ?? 'Unit',
        sourceWarehouseId: item.sourceWarehouseId || raw.sourceWarehouse || null,
        targetWarehouseId: item.targetWarehouseId || raw.targetWarehouse || null,
        isFinishedItem: item.isFinishedItem ?? false,
        materialRequestItemId: item.materialRequestItemId || null,
      })),
    };
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => { this.form.markAsPristine(); this.toaster.success('::SuccessfullyUpdated'); this.router.navigate(['/inventory/stock-entries', this.entityId]); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Update failed'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => { this.form.markAsPristine(); this.toaster.success('::SuccessfullyCreated'); this.router.navigate(['/inventory/stock-entries']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/inventory/stock-entries']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
