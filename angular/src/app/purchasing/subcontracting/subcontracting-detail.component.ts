import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import type { SubcontractingOrderDto, SubcontractingOrderSummaryDto, SubcontractingReceiptSummaryDto } from '../../proxy/purchasing/models';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';
import type { StockReservationEntryDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-subcontracting-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <app-breadcrumb />
    <abp-page [title]="order()?.orderNumber ?? 'Subcontracting Order'">
      @if (!order()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="row mb-3">
          <div class="col-md-8">
            <div class="card">
              <div class="card-header d-flex justify-content-between">
                <h5 class="mb-0">{{ order()!.orderNumber }}</h5>
                <app-status-badge [status]="getOrderStatusLabel(order()!.status)" />
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::Supplier' | abpLocalization }}</small><strong>{{ order()!.supplierName ?? '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::Date' | abpLocalization }}</small><strong>{{ order()!.orderDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ '::GrandTotal' | abpLocalization }}</small><strong class="fs-5">{{ order()!.grandTotal | number:'1.2-2' }}</strong></div>
                  @if (orderSummary(); as os) {
                    <div class="col-md-6">
                      <small class="text-muted d-block">{{ '::Received' | abpLocalization }}</small>
                      <strong>{{ os.totalReceivedQty ?? 0 | number:'1.0-2' }} / {{ os.totalOrderedQty ?? 0 | number:'1.0-2' }} ({{ os.perReceived ?? 0 | number:'1.0-0' }}%)</strong>
                      <div class="progress mt-1" style="height: 5px;">
                        <div class="progress-bar bg-success" [style.width.%]="os.perReceived ?? 0"></div>
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <app-document-workflow [actions]="getActions()" (actionClicked)="onAction($event)" />
          </div>
        </div>

        @if ((order()!.items ?? []).length > 0) {
          <div class="card mb-3">
            <div class="card-header"><h6 class="mb-0">{{ '::Items' | abpLocalization }} (FG)</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>{{ '::Item' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                </tr></thead>
                <tbody>
                  @for (item of order()!.items ?? []; track item.id) {
                    <tr>
                      <td>{{ item.itemName ?? item.itemId }}</td>
                      <td class="text-end">{{ item.qty }}</td>
                      <td class="text-end">
                        <span [class.text-success]="(item.receivedQty ?? 0) >= (item.qty ?? 0)" [class.text-warning]="(item.receivedQty ?? 0) > 0 && (item.receivedQty ?? 0) < (item.qty ?? 0)">
                          {{ item.receivedQty ?? 0 }}
                        </span>
                      </td>
                      <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                      <td class="text-end fw-semibold">{{ (item.qty ?? 0) * (item.rate ?? 0) | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (isTransferring()) {
          <div class="alert alert-info d-flex align-items-center">
            <i class="fa fa-spinner fa-spin me-2"></i>Creating transfer...
          </div>
        }

        @if (receipts().length > 0) {
          <div class="card mb-3">
            <div class="card-header"><h6 class="mb-0">{{ '::Receipts' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>{{ '::ReceiptNumber' | abpLocalization }}</th>
                  <th>{{ '::Date' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ '::NetTotal' | abpLocalization }}</th>
                  <th></th>
                </tr></thead>
                <tbody>
                  @for (r of receipts(); track r.id) {
                    <tr>
                      <td>{{ r.receiptNumber }} @if (r.isReturn) { <span class="badge bg-secondary ms-1">{{ '::Return' | abpLocalization }}</span> }</td>
                      <td>{{ r.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td><app-status-badge [status]="getReceiptStatusLabel(r.status)" /></td>
                      <td class="text-end">{{ r.netTotal | number:'1.2-2' }}</td>
                      <td>
                        <div class="d-flex gap-1">
                          <button class="btn btn-sm btn-outline-info" (click)="toggleReceiptSummary(r.id)">
                            <i class="fa fa-list-check me-1"></i>{{ '::Summary' | abpLocalization }}
                          </button>
                          @if (r.status === 1 && !r.isReturn) {
                            <button class="btn btn-sm btn-outline-danger" (click)="openReturnDialog(r)">
                              <i class="fa fa-rotate-left me-1"></i>{{ '::Return' | abpLocalization }}
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                    @if (selectedReceiptSummary()?.id === r.id) {
                      <tr class="table-light">
                        <td colspan="5" class="small">
                          <span class="me-3"><strong>{{ '::Items' | abpLocalization }}:</strong> {{ selectedReceiptSummary()!.totalItemsCount ?? 0 }}</span>
                          <span class="me-3"><strong>{{ '::TotalReceivedQty' | abpLocalization }}:</strong> {{ selectedReceiptSummary()!.totalReceivedQty ?? 0 | number:'1.0-2' }}</span>
                          <span class="me-3"><strong>{{ '::NetTotal' | abpLocalization }}:</strong> {{ selectedReceiptSummary()!.netTotal ?? 0 | number:'1.2-2' }}</span>
                          @if (selectedReceiptSummary()!.returnAgainstReceiptNumber) {
                            <span><strong>{{ '::Return' | abpLocalization }}:</strong> {{ selectedReceiptSummary()!.returnAgainstReceiptNumber }}</span>
                          }
                        </td>
                      </tr>
                    }
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (showReturnDialog()) {
          <div class="card border-danger mb-3">
            <div class="card-header bg-danger bg-opacity-10 d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-rotate-left me-2"></i>{{ '::ReturnReceipt' | abpLocalization }} — {{ returnAgainst()?.receiptNumber }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showReturnDialog.set(false)"></button>
            </div>
            <div class="card-body">
              @if (canReturnFromRejectedWarehouse) {
                <div class="form-check mb-2">
                  <input class="form-check-input" type="checkbox" id="returnRejectedToggle"
                    [ngModel]="returnFromRejectedWarehouse()" (ngModelChange)="toggleReturnFromRejectedWarehouse($event)">
                  <label class="form-check-label small" for="returnRejectedToggle">
                    {{ '::ReturnFromRejectedWarehouse' | abpLocalization }}
                  </label>
                </div>
              }
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ returnFromRejectedWarehouse() ? ('::RejectedQty' | abpLocalization) : ('::ReceivedQty' | abpLocalization) }}</th>
                    <th class="text-center" style="width: 120px">{{ '::ReturnQty' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of returnItems(); track item.itemId) {
                    <tr>
                      <td>{{ item.itemName }}</td>
                      <td class="text-end">{{ (returnFromRejectedWarehouse() ? item.rejectedQty : item.qty) | number:'1.0-2' }}</td>
                      <td class="text-center">
                        <input type="number" class="form-control form-control-sm text-center"
                          [(ngModel)]="item.returnQty" [max]="returnFromRejectedWarehouse() ? item.rejectedQty : item.qty" min="0" step="0.01">
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <div class="d-flex justify-content-end gap-2 mt-2">
                <button class="btn btn-sm btn-secondary" (click)="showReturnDialog.set(false)">{{ '::Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-danger" (click)="submitReturn()" [disabled]="isReturning() || !hasReturnQty()">
                  @if (isReturning()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                  <i class="fa fa-check me-1"></i>{{ '::ReturnReceipt' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        @if (showReceiptDialog()) {
          <div class="card border-success mb-3">
            <div class="card-header bg-success bg-opacity-10 d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-box-open me-2"></i>{{ '::ReceiveFinishedGoods' | abpLocalization }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showReceiptDialog.set(false)"></button>
            </div>
            <div class="card-body">
              <div class="row mb-3">
                <div class="col-md-5">
                  <label class="form-label">{{ '::Warehouse' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="receiptWarehouseId">
                    <option value="">-- {{ '::SelectWarehouse' | abpLocalization }} --</option>
                    @for (w of warehouses(); track w.id) {
                      <option [value]="w.id">{{ w.warehouseName }}</option>
                    }
                  </select>
                </div>
              </div>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ '::ReceivedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Pending' | abpLocalization }}</th>
                    <th class="text-center" style="width: 120px">{{ '::ReceiveQty' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of receiptItems(); track item.itemId) {
                    <tr>
                      <td>{{ item.itemName }}</td>
                      <td class="text-end">{{ item.orderedQty | number:'1.0-2' }}</td>
                      <td class="text-end">{{ item.receivedQty | number:'1.0-2' }}</td>
                      <td class="text-end" [class.text-warning]="item.pendingQty > 0" [class.text-success]="item.pendingQty <= 0">
                        {{ item.pendingQty | number:'1.0-2' }}
                      </td>
                      <td class="text-center">
                        <input type="number" class="form-control form-control-sm text-center"
                          [(ngModel)]="item.receiveQty" [max]="item.pendingQty" min="0" step="0.01">
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <div class="d-flex justify-content-end gap-2 mt-2">
                <button class="btn btn-sm btn-secondary" (click)="showReceiptDialog.set(false)">{{ '::Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-success" (click)="createReceipt()" [disabled]="isCreatingReceipt() || !hasReceivableQty() || !receiptWarehouseId">
                  @if (isCreatingReceipt()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                  <i class="fa fa-check me-1"></i>{{ '::ReceiveFinishedGoods' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <app-activity-log documentType="SubcontractingOrder" [documentId]="order()!.id!" />
      }

      <!-- Unreserve Stock Modal (ERPNext commits 657ae2b763 & 5e661bcf76) -->
      @if (showUnreserveModal()) {
        <div class="modal d-block" tabindex="-1" style="background: rgba(0,0,0,0.5);">
          <div class="modal-dialog modal-md modal-dialog-centered">
            <div class="modal-content">
              <div class="modal-header py-2">
                <h6 class="modal-title"><i class="fa fa-ban me-2 text-warning"></i>{{ '::UnreserveStock' | abpLocalization }}</h6>
                <button type="button" class="btn-close" (click)="showUnreserveModal.set(false)"></button>
              </div>
              <div class="modal-body p-0">
                <table class="table table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::Item' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Reserved' | abpLocalization }}</th>
                      <th class="text-end">{{ '::HeldQty' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Action' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (sre of heldStockReservations; track sre.id) {
                      <tr>
                        <td>{{ sre.itemId }}</td>
                        <td class="text-end">{{ sre.reservedQty | number:'1.2-2' }}</td>
                        <td class="text-end fw-bold">{{ getHeldQty(sre) | number:'1.2-2' }}</td>
                        <td class="text-end">
                          <button class="btn btn-sm btn-outline-danger" (click)="unreserve(sre)" [disabled]="isUnreserving()">
                            @if (isUnreserving()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                            {{ '::UnreserveStock' | abpLocalization }}
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <div class="modal-footer py-2">
                <button class="btn btn-sm btn-secondary" (click)="showUnreserveModal.set(false)">{{ '::Close' | abpLocalization }}</button>
              </div>
            </div>
          </div>
        </div>
      }
    </abp-page>
  `,
})
export class SubcontractingDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(SubcontractingService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private warehouseService = inject(WarehouseService);
  private stockReservationService = inject(StockReservationService, { optional: true });
  order = signal<SubcontractingOrderDto | null>(null);
  orderSummary = signal<SubcontractingOrderSummaryDto | null>(null);
  stockReservations = signal<StockReservationEntryDto[]>([]);
  showUnreserveModal = signal(false);
  isUnreserving = signal(false);
  isTransferring = signal(false);
  warehouses = signal<{ id: string; warehouseName: string }[]>([]);

  showReceiptDialog = signal(false);
  receiptItems = signal<{ itemId: string; itemName: string; orderedQty: number; receivedQty: number; pendingQty: number; receiveQty: number; rate: number }[]>([]);
  receiptWarehouseId = '';
  isCreatingReceipt = signal(false);

  receipts = signal<any[]>([]);
  selectedReceiptSummary = signal<SubcontractingReceiptSummaryDto | null>(null);
  showReturnDialog = signal(false);
  returnAgainst = signal<any>(null);
  returnFromRejectedWarehouse = signal(false);
  returnItems = signal<{ itemId: string; itemName: string; qty: number; rejectedQty: number; rate: number; returnQty: number; warehouseId?: string; rejectedWarehouseId?: string; returnQtyFromRejectedWarehouse?: boolean }[]>([]);
  isReturning = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.service.getOrder(id).subscribe(o => this.order.set(o));
      this.loadOrderSummary(id);
      this.loadReceipts(id);
      this.loadStockReservations(id);
    }
    this.warehouseService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe({
      next: (r: any) => this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, warehouseName: w.name ?? w.id }))),
      error: () => {}
    });
  }

  loadOrderSummary(subcontractingOrderId: string): void {
    this.service.getOrderSummary(subcontractingOrderId).subscribe({
      next: summary => this.orderSummary.set(summary),
      error: () => this.orderSummary.set(null),
    });
  }

  loadReceipts(subcontractingOrderId: string): void {
    this.service.getReceiptsForOrder(subcontractingOrderId).subscribe({
      next: (r: any) => this.receipts.set(r ?? []),
      error: () => {}
    });
  }

  loadStockReservations(subcontractingOrderId: string): void {
    if (!this.stockReservationService) return;
    this.stockReservationService.getList({
      voucherId: subcontractingOrderId,
      skipCount: 0,
      maxResultCount: 100,
    } as any).subscribe({
      next: res => this.stockReservations.set((res.items ?? []).filter(s => s.status === 1)),
      error: () => this.stockReservations.set([]),
    });
  }

  /**
   * Remaining held reserved quantity on a Stock Reservation Entry
   * (ERPNext commit 657ae2b763: erpnext.stock_reservation.get_held_qty).
   */
  getHeldQty(sre: StockReservationEntryDto): number {
    return Math.max(0, (sre.reservedQty ?? 0) - (sre.deliveredQty ?? 0) - (sre.transferredQty ?? 0) - (sre.consumedQty ?? 0));
  }

  get heldStockReservations(): StockReservationEntryDto[] {
    return this.stockReservations().filter(sre => this.getHeldQty(sre) > 0);
  }

  get hasHeldStockReservation(): boolean {
    return this.heldStockReservations.length > 0;
  }

  openUnreserveModal(): void {
    // Per ERPNext commit 5e661bcf76: say there is nothing to unreserve instead of opening an empty dialog
    if (!this.hasHeldStockReservation) {
      this.toaster.info('No reserved stock to unreserve.');
      this.showUnreserveModal.set(false);
      return;
    }
    this.showUnreserveModal.set(true);
  }

  unreserve(sre: StockReservationEntryDto): void {
    if (!sre.id || !this.stockReservationService) return;
    this.isUnreserving.set(true);
    this.stockReservationService.cancel(sre.id).subscribe({
      next: () => {
        this.isUnreserving.set(false);
        this.showUnreserveModal.set(false);
        this.toaster.success('::SuccessfullyCancelled');
        const id = this.order()?.id;
        if (id) this.loadStockReservations(id);
      },
      error: (err: any) => {
        this.isUnreserving.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  toggleReceiptSummary(receiptId: string): void {
    if (this.selectedReceiptSummary()?.id === receiptId) {
      this.selectedReceiptSummary.set(null);
      return;
    }
    this.service.getReceiptSummary(receiptId).subscribe({
      next: summary => this.selectedReceiptSummary.set(summary),
      error: () => this.toaster.error('::FailedToLoad'),
    });
  }

  /**
   * Per ERPNext PR #67404 (commit ef84f3c6bd): offer rejected-warehouse return only while unreturned rejected qty remains.
   */
  get canReturnFromRejectedWarehouse(): boolean {
    return !!this.returnAgainst()?.hasRejectedQtyToReturn;
  }

  toggleReturnFromRejectedWarehouse(fromRejected: boolean): void {
    const effective = fromRejected && this.canReturnFromRejectedWarehouse;
    this.returnFromRejectedWarehouse.set(effective);
    this.returnItems.update(items => items.map(i => ({
      ...i,
      returnQtyFromRejectedWarehouse: effective,
      returnQty: effective ? i.rejectedQty : i.qty,
      warehouseId: effective ? (i.rejectedWarehouseId || i.warehouseId) : i.warehouseId,
    })));
  }

  openReturnDialog(receipt: any): void {
    this.returnAgainst.set(receipt);
    this.returnFromRejectedWarehouse.set(false);
    // Exclude backend-derived secondary items (Co-Product, By-Product, Scrap) so return UI only collects FG rows
    this.returnItems.set((receipt.items ?? [])
      .filter((i: any) => !i.secondaryItemType && (i.valuationType === undefined || i.valuationType === null) && !i.bomSecondaryItemId)
      .map((i: any) => ({
        itemId: i.itemId,
        itemName: i.itemName,
        qty: i.qty,
        rejectedQty: i.rejectedQty ?? 0,
        rate: i.rate,
        returnQty: i.qty,
        warehouseId: i.warehouseId ?? '',
        rejectedWarehouseId: i.rejectedWarehouseId ?? '',
        returnQtyFromRejectedWarehouse: false,
      })));
    this.showReturnDialog.set(true);
  }

  hasReturnQty(): boolean {
    return this.returnItems().some(i => i.returnQty > 0);
  }

  submitReturn(): void {
    const against = this.returnAgainst();
    const items = this.returnItems().filter(i => i.returnQty > 0);
    if (!against || !items.length) return;
    this.isReturning.set(true);
    this.service.createReceiptReturn({
      returnAgainstReceiptId: against.id,
      postingDate: new Date().toISOString().split('T')[0],
      items: items.map(i => ({
        itemId: i.itemId,
        itemName: i.itemName,
        qty: i.returnQty,
        rate: i.rate,
        warehouseId: i.warehouseId || undefined,
        returnQtyFromRejectedWarehouse: !!i.returnQtyFromRejectedWarehouse,
      })),
    }).subscribe({
      next: (returnReceipt: any) => {
        this.service.submitReceipt(returnReceipt.id).subscribe({
          next: () => {
            this.isReturning.set(false);
            this.showReturnDialog.set(false);
            this.toaster.success('::ReceiptCreatedSuccessfully');
            const id = this.order()!.id!;
            this.loadReceipts(id);
            this.service.getOrder(id).subscribe(o => this.order.set(o));
          },
          error: (err: any) => {
            this.isReturning.set(false);
            this.toaster.error(err?.error?.error?.message || '::OperationFailed');
          },
        });
      },
      error: (err: any) => {
        this.isReturning.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  getActions(): WorkflowAction[] {
    const s = this.order()?.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) actions.push({ name: 'submit', label: '::Submit', icon: 'paper-plane', color: 'primary' });
    if (s === 1 || s === 2) actions.push({ name: 'transferMaterials', label: '::TransferMaterials', icon: 'truck-arrow-right', color: 'success' });
    if (s === 1 || s === 2) actions.push({ name: 'receiveFinishedGoods', label: '::ReceiveFinishedGoods', icon: 'box-open', color: 'success' });
    if (this.hasHeldStockReservation) actions.push({ name: 'unreserveStock', label: '::UnreserveStock', icon: 'ban', color: 'warning' });
    if (s === 1 || s === 2) actions.push({ name: 'close', label: '::Close', icon: 'lock', color: 'dark' });
    if (s === 4) actions.push({ name: 'reopen', label: '::Reopen', icon: 'lock-open', color: 'warning' });
    if (s !== 0 && s !== 5) actions.push({ name: 'cancel', label: '::Cancel', icon: 'ban', color: 'danger' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.order()!.id!;
    const reload = () => this.service.getOrder(id).subscribe(o => this.order.set(o));
    switch (name) {
      case 'submit': this.service.submitOrder(id).subscribe(reload); break;
      case 'transferMaterials': this.createRmTransfer(id); break;
      case 'receiveFinishedGoods': this.openReceiptDialog(); break;
      case 'unreserveStock': this.openUnreserveModal(); break;
      case 'close': this.service.closeOrder(id).subscribe(reload); break;
      case 'reopen': this.service.reopenOrder(id).subscribe(reload); break;
      case 'cancel': this.confirmation.warn('::CancelConfirmationMessage', '::Confirm').subscribe(s => {
        if (s === Confirmation.Status.confirm) this.service.cancelOrder(id).subscribe(reload);
      }); break;
    }
  }

  openReceiptDialog(): void {
    const o = this.order();
    if (!o?.items?.length) return;
    const items = o.items
      .map(i => ({
        itemId: i.itemId!,
        itemName: i.itemName ?? i.itemId!,
        orderedQty: i.qty ?? 0,
        receivedQty: i.receivedQty ?? 0,
        pendingQty: Math.max(0, (i.qty ?? 0) - (i.receivedQty ?? 0)),
        receiveQty: Math.max(0, (i.qty ?? 0) - (i.receivedQty ?? 0)),
        rate: i.rate ?? 0,
      }))
      .filter(i => i.pendingQty > 0);
    this.receiptItems.set(items);
    this.receiptWarehouseId = '';
    this.showReceiptDialog.set(true);
  }

  hasReceivableQty(): boolean {
    return this.receiptItems().some(i => i.receiveQty > 0);
  }

  createReceipt(): void {
    const o = this.order();
    if (!o || !this.receiptWarehouseId) return;
    const items = this.receiptItems().filter(i => i.receiveQty > 0);
    if (!items.length) return;
    this.isCreatingReceipt.set(true);
    this.service.createReceipt({
      companyId: o.companyId!,
      supplierId: o.supplierId!,
      subcontractingOrderId: o.id!,
      postingDate: new Date().toISOString().split('T')[0],
      warehouseId: this.receiptWarehouseId,
      supplierWarehouseId: o.supplierWarehouseId || undefined,
      items: items.map(i => ({
        itemId: i.itemId, itemName: i.itemName, qty: i.receiveQty, rate: i.rate,
        warehouseId: this.receiptWarehouseId,
      })),
    }).subscribe({
      next: (receipt: any) => {
        this.service.submitReceipt(receipt.id).subscribe({
          next: () => {
            this.isCreatingReceipt.set(false);
            this.showReceiptDialog.set(false);
            this.toaster.success('::ReceiptCreatedSuccessfully');
            this.service.getOrder(o.id!).subscribe(updated => this.order.set(updated));
            this.loadReceipts(o.id!);
          },
          error: (err: any) => {
            this.isCreatingReceipt.set(false);
            this.toaster.error(err?.error?.error?.message || '::OperationFailed');
          },
        });
      },
      error: (err: any) => {
        this.isCreatingReceipt.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  createRmTransfer(scoId: string): void {
    this.isTransferring.set(true);
    this.service.createRmTransferStockEntry(scoId).subscribe({
      next: (result: any) => {
        this.isTransferring.set(false);
        this.toaster.success(`Stock Entry ${result.entryNumber} created with ${result.itemCount} items`);
        this.router.navigate(['/inventory/stock-entries', result.stockEntryId]);
      },
      error: (err: any) => {
        this.isTransferring.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }

  getOrderStatusLabel(status?: number): string {
    return ['Draft', 'Open', 'Partially Received', 'Completed', 'Closed', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  getReceiptStatusLabel(status?: number): string {
    return ['Draft', 'Submitted', 'Cancelled'][status ?? 0] ?? 'Draft';
  }

  /**
   * Computes raw material transfer status per distinct raw material itemId across all supplied item rows
   * (per ERPNext commit e9d264a6a4: get_transfer_status).
   */
  getMaterialTransferStatus(
    suppliedItems: { itemId: string; requiredQty?: number; transferredQty?: number }[] | null | undefined,
    precision = 4,
  ): 'Open' | 'Partial Material Transferred' | 'Material Transferred' {
    if (!suppliedItems || suppliedItems.length === 0) return 'Open';
    const requiredByRm = new Map<string, number>();
    const suppliedByRm = new Map<string, number>();
    for (const row of suppliedItems) {
      if (!row?.itemId) continue;
      requiredByRm.set(row.itemId, (requiredByRm.get(row.itemId) ?? 0) + Number(row.requiredQty ?? 0));
      suppliedByRm.set(row.itemId, (suppliedByRm.get(row.itemId) ?? 0) + Number(row.transferredQty ?? 0));
    }
    const anySupplied = Array.from(suppliedByRm.values()).some(q => q > 0);
    if (!anySupplied) return 'Open';

    const factor = Math.pow(10, precision);
    for (const [rmId, reqQty] of requiredByRm.entries()) {
      const supQty = suppliedByRm.get(rmId) ?? 0;
      const roundedSup = Math.round((supQty + Number.EPSILON) * factor) / factor;
      const roundedReq = Math.round((reqQty + Number.EPSILON) * factor) / factor;
      if (roundedSup < roundedReq) {
        return 'Partial Material Transferred';
      }
    }
    return 'Material Transferred';
  }

  /**
   * Removes only the service item row paired with the deleted finished good row's purchaseOrderItemId
   * (falling back to fgItemId only when purchaseOrderItemId is unset) so sibling rows for the same FG item
   * on other PO rows keep their service items (per ERPNext commit 4211b7d593).
   */
  removePairedServiceItemByPurchaseOrderItem<T extends { purchaseOrderItemId?: string | null; fgItemId: string }>(
    deletedPurchaseOrderItemId: string | null | undefined,
    deletedFgItemId: string,
    serviceItems: T[] | null | undefined,
  ): T[] {
    if (!serviceItems) return [];
    if (deletedPurchaseOrderItemId) {
      return serviceItems.filter(s => s.purchaseOrderItemId !== deletedPurchaseOrderItemId);
    }
    return serviceItems.filter(s => s.fgItemId !== deletedFgItemId);
  }
}
