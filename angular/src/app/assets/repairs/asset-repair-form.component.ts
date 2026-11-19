import { Component, computed, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetStatus } from '../../proxy/assets/asset-status.enum';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ProjectService } from '../../proxy/projects/project.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type {
  AssetDto,
  AssetRepairExpenseAccountDto,
  CreateUpdateAssetRepairConsumedItemDto,
  CreateUpdateAssetRepairPurchaseInvoiceDto,
} from '../../proxy/assets/models';
import type { CostCenterDto } from '../../proxy/accounting/models';
import type { ItemDto, WarehouseDto } from '../../proxy/inventory/models';
import type { ProjectDto } from '../../proxy/projects/models';
import type { PurchaseInvoiceDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-asset-repair-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="(isEdit() ? '::EditAssetRepair' : '::NewRepair') | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::Asset' | abpLocalization }} *</label>
                <select class="form-select" formControlName="assetId">
                  <option value="">— Select Asset —</option>
                  @for (a of assets(); track a.id) {
                    <option [value]="a.id">{{ a.assetName }}</option>
                  }
                </select>
                @if (selectedAssetFullyDepreciated()) {
                  <small class="text-muted">{{ '::AssetFullyDepreciatedRepairHint' | abpLocalization }}</small>
                }
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::FailureDate' | abpLocalization }} *</label>
                <input type="date" class="form-control" formControlName="failureDate" />
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::CompletionDate' | abpLocalization }}</label>
                <input type="date" class="form-control" formControlName="completionDate" />
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::Description' | abpLocalization }}</label>
                <textarea class="form-control" rows="2" formControlName="repairDescription"></textarea>
              </div>
              <div class="col-12">
                <label class="form-label">{{ '::ActionsPerformed' | abpLocalization }}</label>
                <textarea class="form-control" rows="2" formControlName="actionsPerformed"></textarea>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::Downtime' | abpLocalization }}</label>
                <input type="text" class="form-control" formControlName="downtime" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::RepairCost' | abpLocalization }}</label>
                <input type="number" min="0" step="0.01" class="form-control" formControlName="repairCost" />
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::CostCenter' | abpLocalization }}</label>
                <select class="form-select" formControlName="costCenterId">
                  <option [ngValue]="null">—</option>
                  @for (cc of costCenters(); track cc.id) {
                    <option [ngValue]="cc.id">{{ cc.name }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::Project' | abpLocalization }}</label>
                <select class="form-select" formControlName="projectId">
                  <option [ngValue]="null">—</option>
                  @for (p of projects(); track p.id) {
                    <option [ngValue]="p.id">{{ p.projectName }}</option>
                  }
                </select>
              </div>
              <div class="col-md-4 d-flex align-items-end">
                <div class="form-check">
                  <input type="checkbox" class="form-check-input" id="capitalize"
                    formControlName="capitalizeRepairCost" [attr.disabled]="selectedAssetFullyDepreciated() ? true : null">
                  <label class="form-check-label" for="capitalize">{{ '::Capitalize' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-4">
                <label class="form-label">{{ '::IncreaseInAssetLifeMonths' | abpLocalization }}</label>
                <input type="number" min="0" step="1" class="form-control" formControlName="increaseInAssetLife"
                  [attr.disabled]="(!form.value.capitalizeRepairCost || selectedAssetFullyDepreciated()) ? true : null" />
              </div>
            </div>

            <div class="mt-4">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <h6 class="mb-0">{{ '::ConsumedStockItems' | abpLocalization }}</h6>
                <button type="button" class="btn btn-sm btn-outline-primary" (click)="addStockItemRow()">
                  <i class="fa fa-plus me-1"></i>{{ '::AddRow' | abpLocalization }}
                </button>
              </div>
              @if (stockItems().length > 0) {
                <div class="table-responsive">
                  <table class="table table-sm table-bordered align-middle mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::Item' | abpLocalization }}</th>
                        <th>{{ '::Warehouse' | abpLocalization }}</th>
                        <th style="width: 110px;">{{ '::Qty' | abpLocalization }}</th>
                        <th style="width: 140px;">{{ '::ValuationRate' | abpLocalization }}</th>
                        <th style="width: 140px;" class="text-end">{{ '::TotalValue' | abpLocalization }}</th>
                        <th style="width: 48px;"></th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of stockItems(); track $index) {
                        <tr>
                          <td>
                            <select class="form-select form-select-sm"
                              [ngModel]="row.itemId ?? ''"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="onStockItemChange($index, $event)">
                              <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                              @for (it of items(); track it.id) {
                                <option [value]="it.id">{{ it.itemCode }} — {{ it.itemName }}</option>
                              }
                            </select>
                          </td>
                          <td>
                            <select class="form-select form-select-sm"
                              [ngModel]="row.warehouseId ?? ''"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="onStockWarehouseChange($index, $event)">
                              <option value="">{{ '::SelectWarehouse' | abpLocalization }}</option>
                              @for (wh of warehouses(); track wh.id) {
                                <option [value]="wh.id">{{ wh.name }}</option>
                              }
                            </select>
                          </td>
                          <td>
                            <input type="number" min="0.0001" step="0.01" class="form-control form-control-sm text-end"
                              [ngModel]="row.qty ?? 1"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="onStockQtyChange($index, +$event || 0)" />
                          </td>
                          <td>
                            <input type="number" min="0" step="0.01" class="form-control form-control-sm text-end"
                              [ngModel]="row.valuationRate ?? 0"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="updateStockItemRow($index, { valuationRate: +$event || 0 })" />
                          </td>
                          <td class="text-end fw-semibold">
                            {{ ((row.qty ?? 0) * (row.valuationRate ?? 0)) | number:'1.2-2' }}
                          </td>
                          <td class="text-center">
                            <button type="button" class="btn btn-sm btn-outline-danger" (click)="removeStockItemRow($index)">
                              <i class="fa fa-trash"></i>
                            </button>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
                <div class="d-flex justify-content-end gap-4 mt-2 small text-muted">
                  <span>{{ '::ConsumedItemsCost' | abpLocalization }}: <strong>{{ consumedItemsCost() | number:'1.2-2' }}</strong></span>
                  <span>{{ '::TotalRepairCost' | abpLocalization }}: <strong class="text-body">{{ totalRepairCost() | number:'1.2-2' }}</strong></span>
                </div>
              }
            </div>

            <div class="mt-4">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <h6 class="mb-0">{{ '::PurchaseInvoices' | abpLocalization }}</h6>
                <button type="button" class="btn btn-sm btn-outline-primary" (click)="addInvoiceRow()">
                  <i class="fa fa-plus me-1"></i>{{ '::AddRow' | abpLocalization }}
                </button>
              </div>
              @if (invoices().length > 0) {
                <div class="table-responsive">
                  <table class="table table-sm table-bordered align-middle mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::PurchaseInvoice' | abpLocalization }}</th>
                        <th>{{ '::ExpenseAccount' | abpLocalization }}</th>
                        <th style="width: 180px;">{{ '::RepairCost' | abpLocalization }}</th>
                        <th style="width: 48px;"></th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of invoices(); track $index) {
                        <tr>
                          <td>
                            <select class="form-select form-select-sm"
                              [ngModel]="row.purchaseInvoiceId ?? ''"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="onInvoiceChange($index, $event)">
                              <option value="">— Select Purchase Invoice —</option>
                              @for (pi of purchaseInvoices(); track pi.id) {
                                <option [value]="pi.id">{{ pi.invoiceNumber }} — {{ pi.supplierName }}</option>
                              }
                            </select>
                          </td>
                          <td>
                            <select class="form-select form-select-sm"
                              [ngModel]="row.expenseAccountId ?? ''"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="updateInvoiceRow($index, { expenseAccountId: $event || null })">
                              <option value="">— Select Expense Account —</option>
                              @for (acc of getExpenseAccounts(row.purchaseInvoiceId); track acc.accountId) {
                                <option [value]="acc.accountId">
                                  {{ acc.accountCode }} — {{ acc.accountName }} ({{ acc.unallocatedAmount | number:'1.2-2' }})
                                </option>
                              }
                            </select>
                          </td>
                          <td>
                            <input type="number" min="0" step="0.01" class="form-control form-control-sm text-end"
                              [ngModel]="row.repairCost ?? 0"
                              [ngModelOptions]="{ standalone: true }"
                              (ngModelChange)="updateInvoiceRow($index, { repairCost: +$event || 0 })" />
                          </td>
                          <td class="text-center">
                            <button type="button" class="btn btn-sm btn-outline-danger" (click)="removeInvoiceRow($index)">
                              <i class="fa fa-trash"></i>
                            </button>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ '::Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" [routerLink]="cancelLink()">{{ '::Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class AssetRepairFormComponent implements OnInit {
  private service = inject(AssetRepairService);
  private assetService = inject(AssetService);
  private costCenterService = inject(CostCenterService);
  private itemService = inject(ItemService);
  private warehouseService = inject(WarehouseService);
  private projectService = inject(ProjectService);
  private purchaseInvoiceService = inject(PurchaseInvoiceService);
  private companyContext = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  assets = signal<AssetDto[]>([]);
  costCenters = signal<CostCenterDto[]>([]);
  items = signal<ItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  projects = signal<ProjectDto[]>([]);
  purchaseInvoices = signal<PurchaseInvoiceDto[]>([]);
  stockItems = signal<CreateUpdateAssetRepairConsumedItemDto[]>([]);
  invoices = signal<CreateUpdateAssetRepairPurchaseInvoiceDto[]>([]);
  expenseAccountsByInvoice = signal<Record<string, AssetRepairExpenseAccountDto[]>>({});
  isSaving = signal(false);
  repairId = signal<string | null>(null);
  isEdit = computed(() => !!this.repairId());
  cancelLink = computed(() => this.repairId() ? `/assets/repairs/${this.repairId()}` : '/assets/repairs');
  consumedItemsCost = computed(() =>
    this.stockItems().reduce((sum, row) => sum + ((row.qty ?? 0) * (row.valuationRate ?? 0)), 0),
  );
  totalRepairCost = computed(() => {
    const invoiceTotal = this.invoices().reduce((sum, inv) => sum + (inv.repairCost ?? 0), 0);
    const baseRepairCost = this.invoices().length > 0 ? invoiceTotal : (Number(this.form.get('repairCost')?.value) || 0);
    return baseRepairCost + this.consumedItemsCost();
  });

  form = this.fb.group({
    assetId: ['', Validators.required],
    failureDate: [new Date().toISOString().split('T')[0], Validators.required],
    completionDate: [null as string | null],
    repairDescription: [''],
    actionsPerformed: [''],
    downtime: [''],
    repairCost: [0],
    costCenterId: [null as string | null],
    projectId: [null as string | null],
    capitalizeRepairCost: [false],
    increaseInAssetLife: [0],
  });

  selectedAssetFullyDepreciated = computed(() => {
    const id = this.form.get('assetId')?.value;
    const asset = this.assets().find(a => a.id === id);
    return !!asset && (asset.isFullyDepreciated || asset.status === AssetStatus.FullyDepreciated);
  });

  ngOnInit(): void {
    this.assetService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.assets.set((r.items ?? []).filter(a => a.status !== AssetStatus.Sold && a.status !== AssetStatus.Scrapped));
    });
    this.costCenterService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any).subscribe(r => {
      this.costCenters.set(r.items ?? []);
    });
    this.projectService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any).subscribe(r => {
      this.projects.set(r.items ?? []);
    });
    const cid = this.companyContext.currentCompanyId();
    this.itemService.getList({ companyId: cid ?? undefined, skipCount: 0, maxResultCount: 500 } as any).subscribe(r => {
      this.items.set((r.items ?? []).filter(i => i.maintainStock !== false));
    });
    this.warehouseService.getList({ companyId: cid ?? undefined, skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.warehouses.set((r.items ?? []).filter(w => !w.isGroup));
    });
    this.purchaseInvoiceService.getList({ companyId: cid ?? undefined, skipCount: 0, maxResultCount: 200 } as any).subscribe(r => {
      this.purchaseInvoices.set((r.items ?? []).filter(
        pi => pi.status === 'Submitted' || pi.status === 'Posted'
      ));
    });

    this.form.get('completionDate')?.valueChanges.subscribe(() => {
      this.stockItems().forEach((row, idx) => {
        if (row.itemId && row.warehouseId && !row.serialAndBatchBundleId) {
          this.fetchConsumedItemRate(idx);
        }
      });
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.repairId.set(id);
      this.service.get(id).subscribe(repair => {
        this.form.patchValue({
          assetId: repair.assetId,
          failureDate: repair.failureDate ? repair.failureDate.split('T')[0] : null,
          completionDate: repair.completionDate ? repair.completionDate.split('T')[0] : null,
          repairDescription: repair.repairDescription ?? '',
          actionsPerformed: repair.actionsPerformed ?? '',
          downtime: repair.downtime ?? '',
          repairCost: repair.repairCost,
          costCenterId: repair.costCenterId ?? null,
          projectId: repair.projectId ?? null,
          capitalizeRepairCost: repair.capitalizeRepairCost,
          increaseInAssetLife: repair.increaseInAssetLife,
        } as any, { emitEvent: false });
        const existingStockItems = (repair.stockItems ?? []).map(si => ({
          id: si.id,
          itemId: si.itemId,
          itemName: si.itemName ?? null,
          warehouseId: si.warehouseId ?? null,
          qty: si.qty ?? 1,
          valuationRate: si.valuationRate ?? 0,
          serialAndBatchBundleId: si.serialAndBatchBundleId ?? null,
        }));
        this.stockItems.set(existingStockItems);
        const existingInvoices = (repair.invoices ?? []).map(inv => ({
          id: inv.id,
          purchaseInvoiceId: inv.purchaseInvoiceId,
          purchaseInvoiceNumber: inv.purchaseInvoiceNumber,
          expenseAccountId: inv.expenseAccountId ?? null,
          repairCost: inv.repairCost ?? 0,
        }));
        this.invoices.set(existingInvoices);
        for (const inv of existingInvoices) {
          if (inv.purchaseInvoiceId) {
            this.loadExpenseAccounts(inv.purchaseInvoiceId);
          }
        }
      });
    }
  }

  addStockItemRow(): void {
    this.stockItems.update(rows => [
      ...rows,
      { itemId: '', itemName: null, warehouseId: null, qty: 1, valuationRate: 0, serialAndBatchBundleId: null },
    ]);
  }

  removeStockItemRow(index: number): void {
    this.stockItems.update(rows => rows.filter((_, i) => i !== index));
  }

  updateStockItemRow(index: number, patch: Partial<CreateUpdateAssetRepairConsumedItemDto>): void {
    this.stockItems.update(rows => rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  onStockItemChange(index: number, itemId: string): void {
    const selected = this.items().find(i => i.id === itemId);
    this.updateStockItemRow(index, {
      itemId,
      itemName: selected?.itemName ?? null,
    });
    const row = this.stockItems()[index];
    if (itemId && row?.warehouseId && !row.serialAndBatchBundleId) {
      this.fetchConsumedItemRate(index);
    }
  }

  onStockWarehouseChange(index: number, warehouseId: string): void {
    this.updateStockItemRow(index, { warehouseId: warehouseId || null });
    const row = this.stockItems()[index];
    if (row?.itemId && warehouseId && !row.serialAndBatchBundleId) {
      this.fetchConsumedItemRate(index);
    }
  }

  onStockQtyChange(index: number, qty: number): void {
    this.updateStockItemRow(index, { qty });
    const row = this.stockItems()[index];
    // Per ERPNext commit 8adb2c3507: FIFO outgoing rate depends on consumed qty, so re-fetch when warehouse is set.
    if (row?.itemId && row.warehouseId && qty > 0 && !row.serialAndBatchBundleId) {
      this.fetchConsumedItemRate(index);
    }
  }

  fetchConsumedItemRate(index: number): void {
    const row = this.stockItems()[index];
    if (!row?.itemId || !row.warehouseId) return;
    const postingDate = this.form.get('completionDate')?.value || null;
    this.service.getConsumedItemValuationRate({
      itemId: row.itemId,
      warehouseId: row.warehouseId,
      qty: row.qty && row.qty > 0 ? row.qty : 1,
      postingDate,
      serialAndBatchBundleId: row.serialAndBatchBundleId ?? null,
    }).subscribe(rate => {
      this.updateStockItemRow(index, { valuationRate: Number(rate) || 0 });
    });
  }

  addInvoiceRow(): void {
    this.invoices.update(rows => [...rows, { purchaseInvoiceId: '', purchaseInvoiceNumber: null, expenseAccountId: null, repairCost: 0 }]);
  }

  removeInvoiceRow(index: number): void {
    this.invoices.update(rows => rows.filter((_, i) => i !== index));
  }

  updateInvoiceRow(index: number, patch: Partial<CreateUpdateAssetRepairPurchaseInvoiceDto>): void {
    this.invoices.update(rows => rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  getExpenseAccounts(purchaseInvoiceId?: string): AssetRepairExpenseAccountDto[] {
    if (!purchaseInvoiceId) return [];
    return this.expenseAccountsByInvoice()[purchaseInvoiceId] ?? [];
  }

  onInvoiceChange(index: number, purchaseInvoiceId: string): void {
    const selected = this.purchaseInvoices().find(pi => pi.id === purchaseInvoiceId);
    this.updateInvoiceRow(index, {
      purchaseInvoiceId,
      purchaseInvoiceNumber: selected?.invoiceNumber ?? null,
      expenseAccountId: null,
    });
    if (!purchaseInvoiceId) return;

    this.service.getExpenseAccountsForPurchaseInvoice(purchaseInvoiceId).subscribe(accounts => {
      this.expenseAccountsByInvoice.update(map => ({ ...map, [purchaseInvoiceId]: accounts ?? [] }));
      if ((accounts?.length ?? 0) === 1 && accounts[0].accountId) {
        this.updateInvoiceRow(index, { expenseAccountId: accounts[0].accountId });
      }
    });
  }

  private loadExpenseAccounts(purchaseInvoiceId: string): void {
    if (this.expenseAccountsByInvoice()[purchaseInvoiceId]) return;
    this.service.getExpenseAccountsForPurchaseInvoice(purchaseInvoiceId).subscribe(accounts => {
      this.expenseAccountsByInvoice.update(map => ({ ...map, [purchaseInvoiceId]: accounts ?? [] }));
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.isSaving.set(true);
    const val = this.form.getRawValue();
    const cid = this.companyContext.currentCompanyId();
    const validStockItems = this.stockItems().filter(si => !!si.itemId && !!si.warehouseId && (si.qty ?? 0) > 0);
    const validInvoices = this.invoices().filter(inv => !!inv.purchaseInvoiceId);
    const payload = { ...val, companyId: cid ?? undefined, stockItems: validStockItems, invoices: validInvoices };

    const id = this.repairId();
    const request$ = id ? this.service.update(id, payload as any) : this.service.create(payload as any);
    request$.subscribe({
      next: (result) => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/repairs', result.id]);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
