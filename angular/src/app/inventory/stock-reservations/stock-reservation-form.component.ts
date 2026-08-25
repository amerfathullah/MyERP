import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { SalesOrderDto, SalesOrderItemDto } from '../../proxy/sales/models';
import type { WarehouseDto } from '../../proxy/inventory/models';

const UNRESERVABLE_SO_STATUSES = ['Draft', 'Cancelled', 'Closed'];

@Component({
  selector: 'app-stock-reservation-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewStockReservation' | abpLocalization">
      <div class="card">
        <div class="card-body">
          <form [formGroup]="form" (ngSubmit)="save()">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ 'SalesOrder' | abpLocalization }} *</label>
                <select class="form-select" formControlName="salesOrderId" (change)="onSalesOrderChange()">
                  <option value="">— Select Sales Order —</option>
                  @for (so of salesOrders(); track so.id) {
                    <option [value]="so.id">{{ so.orderNumber }}</option>
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'SalesOrderItem' | abpLocalization }} *</label>
                <select class="form-select" formControlName="soItemId" (change)="onSoItemChange()"
                  [attr.disabled]="soItems().length === 0 ? true : null">
                  <option value="">— Select Item —</option>
                  @for (item of soItems(); track item.id) {
                    <option [value]="item.id">{{ item.description }} ({{ pendingQty(item) | number:'1.2-2' }} pending)</option>
                  }
                </select>
                @if (selectedSoItem() && !selectedSoItem()!.warehouseId) {
                  <small class="text-danger">{{ 'ItemHasNoWarehouseCannotReserve' | abpLocalization }}</small>
                }
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'Warehouse' | abpLocalization }}</label>
                <input type="text" class="form-control" [value]="selectedWarehouseName()" disabled />
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ 'ReservedQty' | abpLocalization }} *</label>
                <input type="number" min="0.0001" step="0.01" class="form-control" formControlName="reservedQty" />
                @if (selectedSoItem()) {
                  <small class="text-muted">{{ 'PendingQty' | abpLocalization }}: {{ pendingQty(selectedSoItem()!) | number:'1.2-2' }}</small>
                }
              </div>
            </div>
            <hr />
            <div class="d-flex gap-2">
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving() || !canSubmit()">
                @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                {{ 'Save' | abpLocalization }}
              </button>
              <a class="btn btn-secondary" routerLink="/inventory/stock-reservations">{{ 'Cancel' | abpLocalization }}</a>
            </div>
          </form>
        </div>
      </div>
    </abp-page>
  `,
})
export class StockReservationFormComponent implements OnInit {
  private service = inject(StockReservationService);
  private salesOrderService = inject(SalesOrderService);
  private warehouseService = inject(WarehouseService);
  private companyContext = inject(CompanyContextService);
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  salesOrders = signal<SalesOrderDto[]>([]);
  soItems = signal<SalesOrderItemDto[]>([]);
  warehouses = signal<WarehouseDto[]>([]);
  isSaving = signal(false);

  form = this.fb.group({
    salesOrderId: ['', Validators.required],
    soItemId: ['', Validators.required],
    reservedQty: [0, [Validators.required, Validators.min(0.0001)]],
  });

  selectedSoItem = computed(() => {
    const id = this.form.get('soItemId')?.value;
    return this.soItems().find(i => i.id === id) ?? null;
  });

  selectedWarehouseName = computed(() => {
    const item = this.selectedSoItem();
    if (!item?.warehouseId) return '';
    return this.warehouses().find(w => w.id === item.warehouseId)?.name ?? item.warehouseId;
  });

  canSubmit = computed(() => !!this.selectedSoItem()?.warehouseId);

  pendingQty(item: SalesOrderItemDto): number {
    return (item.quantity ?? 0) - (item.deliveredQty ?? 0);
  }

  ngOnInit(): void {
    const cid = this.companyContext.currentCompanyId();
    this.salesOrderService.getList({ skipCount: 0, maxResultCount: 200, companyId: cid ?? undefined } as any).subscribe(r => {
      this.salesOrders.set((r.items ?? []).filter((so: any) => !UNRESERVABLE_SO_STATUSES.includes(so.status)));
    });
    this.warehouseService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' }).subscribe(r => {
      this.warehouses.set(r.items ?? []);
    });
  }

  onSalesOrderChange(): void {
    const soId = this.form.get('salesOrderId')?.value;
    this.form.patchValue({ soItemId: '', reservedQty: 0 });
    this.soItems.set([]);
    if (!soId) return;

    this.salesOrderService.get(soId).subscribe(so => {
      this.soItems.set((so.items ?? []).filter(i => this.pendingQty(i) > 0));
    });
  }

  onSoItemChange(): void {
    const item = this.selectedSoItem();
    this.form.patchValue({ reservedQty: item ? this.pendingQty(item) : 0 });
  }

  save(): void {
    if (this.form.invalid) return;
    const item = this.selectedSoItem();
    const soId = this.form.get('salesOrderId')?.value;
    if (!item?.warehouseId || !soId) return;

    const so = this.salesOrders().find(s => s.id === soId);
    if (!so) return;

    this.isSaving.set(true);
    this.service.create({
      companyId: so.companyId,
      itemId: item.itemId,
      warehouseId: item.warehouseId,
      voucherType: 'SalesOrder',
      voucherId: soId,
      voucherDetailId: item.id,
      reservedQty: this.form.get('reservedQty')!.value,
    } as any).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/inventory/stock-reservations']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
