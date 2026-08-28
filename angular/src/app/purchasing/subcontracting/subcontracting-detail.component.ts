import { Component, inject, OnInit, signal } from '@angular/core';
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
import type { SubcontractingOrderDto } from '../../proxy/purchasing/models';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';

@Component({
  selector: 'app-subcontracting-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
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
                <app-status-badge [status]="order()!.status + ''" />
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Supplier' | abpLocalization }}</small><strong>{{ order()!.supplierName ?? '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Date' | abpLocalization }}</small><strong>{{ order()!.orderDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'GrandTotal' | abpLocalization }}</small><strong class="fs-5">{{ order()!.grandTotal | number:'1.2-2' }}</strong></div>
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
            <div class="card-header"><h6 class="mb-0">{{ 'Items' | abpLocalization }} (FG)</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ReceivedQty' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
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
            <div class="card-header"><h6 class="mb-0">{{ 'Receipts' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>{{ 'ReceiptNumber' | abpLocalization }}</th>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th class="text-end">{{ 'NetTotal' | abpLocalization }}</th>
                  <th></th>
                </tr></thead>
                <tbody>
                  @for (r of receipts(); track r.id) {
                    <tr>
                      <td>{{ r.receiptNumber }} @if (r.isReturn) { <span class="badge bg-secondary ms-1">{{ 'Return' | abpLocalization }}</span> }</td>
                      <td>{{ r.postingDate | date:'dd/MM/yyyy' }}</td>
                      <td><app-status-badge [status]="r.status + ''" /></td>
                      <td class="text-end">{{ r.netTotal | number:'1.2-2' }}</td>
                      <td>
                        @if (r.status === 1 && !r.isReturn) {
                          <button class="btn btn-sm btn-outline-danger" (click)="openReturnDialog(r)">
                            <i class="fa fa-rotate-left me-1"></i>{{ 'Return' | abpLocalization }}
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        @if (showReturnDialog()) {
          <div class="card border-danger mb-3">
            <div class="card-header bg-danger bg-opacity-10 d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-rotate-left me-2"></i>{{ 'ReturnReceipt' | abpLocalization }} — {{ returnAgainst()?.receiptNumber }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showReturnDialog.set(false)"></button>
            </div>
            <div class="card-body">
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end">{{ 'ReceivedQty' | abpLocalization }}</th>
                    <th class="text-center" style="width: 120px">{{ 'ReturnQty' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of returnItems(); track item.itemId) {
                    <tr>
                      <td>{{ item.itemName }}</td>
                      <td class="text-end">{{ item.qty | number:'1.0-2' }}</td>
                      <td class="text-center">
                        <input type="number" class="form-control form-control-sm text-center"
                          [(ngModel)]="item.returnQty" [max]="item.qty" min="0" step="0.01">
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
              <div class="d-flex justify-content-end gap-2 mt-2">
                <button class="btn btn-sm btn-secondary" (click)="showReturnDialog.set(false)">{{ 'Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-danger" (click)="submitReturn()" [disabled]="isReturning() || !hasReturnQty()">
                  @if (isReturning()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                  <i class="fa fa-check me-1"></i>{{ 'ReturnReceipt' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        @if (showReceiptDialog()) {
          <div class="card border-success mb-3">
            <div class="card-header bg-success bg-opacity-10 d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-box-open me-2"></i>{{ 'ReceiveFinishedGoods' | abpLocalization }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showReceiptDialog.set(false)"></button>
            </div>
            <div class="card-body">
              <div class="row mb-3">
                <div class="col-md-5">
                  <label class="form-label">{{ 'Warehouse' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="receiptWarehouseId">
                    <option value="">-- {{ 'SelectWarehouse' | abpLocalization }} --</option>
                    @for (w of warehouses(); track w.id) {
                      <option [value]="w.id">{{ w.warehouseName }}</option>
                    }
                  </select>
                </div>
              </div>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'Item' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                    <th class="text-end">{{ 'ReceivedQty' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Pending' | abpLocalization }}</th>
                    <th class="text-center" style="width: 120px">{{ 'ReceiveQty' | abpLocalization }}</th>
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
                <button class="btn btn-sm btn-secondary" (click)="showReceiptDialog.set(false)">{{ 'Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-success" (click)="createReceipt()" [disabled]="isCreatingReceipt() || !hasReceivableQty() || !receiptWarehouseId">
                  @if (isCreatingReceipt()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                  <i class="fa fa-check me-1"></i>{{ 'ReceiveFinishedGoods' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <app-activity-log documentType="SubcontractingOrder" [documentId]="order()!.id!" />
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
  order = signal<SubcontractingOrderDto | null>(null);
  isTransferring = signal(false);
  warehouses = signal<{ id: string; warehouseName: string }[]>([]);

  showReceiptDialog = signal(false);
  receiptItems = signal<{ itemId: string; itemName: string; orderedQty: number; receivedQty: number; pendingQty: number; receiveQty: number; rate: number }[]>([]);
  receiptWarehouseId = '';
  isCreatingReceipt = signal(false);

  receipts = signal<any[]>([]);
  showReturnDialog = signal(false);
  returnAgainst = signal<any>(null);
  returnItems = signal<{ itemId: string; itemName: string; qty: number; rate: number; returnQty: number }[]>([]);
  isReturning = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.service.getOrder(id).subscribe(o => this.order.set(o));
      this.loadReceipts(id);
    }
    this.warehouseService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe({
      next: (r: any) => this.warehouses.set((r.items ?? []).map((w: any) => ({ id: w.id, warehouseName: w.name ?? w.id }))),
      error: () => {}
    });
  }

  loadReceipts(subcontractingOrderId: string): void {
    this.service.getReceiptsForOrder(subcontractingOrderId).subscribe({
      next: (r: any) => this.receipts.set(r ?? []),
      error: () => {}
    });
  }

  openReturnDialog(receipt: any): void {
    this.returnAgainst.set(receipt);
    this.returnItems.set((receipt.items ?? []).map((i: any) => ({
      itemId: i.itemId, itemName: i.itemName, qty: i.qty, rate: i.rate, returnQty: i.qty,
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
      items: items.map(i => ({ itemId: i.itemId, itemName: i.itemName, qty: i.returnQty, rate: i.rate })),
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
    if (s === 0) actions.push({ name: 'submit', label: 'Submit', icon: 'fa-paper-plane', color: 'btn-outline-primary' });
    if (s === 1 || s === 2) actions.push({ name: 'transferMaterials', label: 'Transfer Materials', icon: 'fa-truck-arrow-right', color: 'btn-outline-success' });
    if (s === 1 || s === 2) actions.push({ name: 'receiveFinishedGoods', label: 'Receive Finished Goods', icon: 'fa-box-open', color: 'btn-outline-success' });
    if (s === 1 || s === 2) actions.push({ name: 'close', label: 'Close', icon: 'fa-lock', color: 'btn-outline-dark' });
    if (s === 4) actions.push({ name: 'reopen', label: 'Reopen', icon: 'fa-lock-open', color: 'btn-outline-warning' });
    if (s !== 0 && s !== 5) actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa-ban', color: 'btn-outline-danger' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.order()!.id!;
    const reload = () => this.service.getOrder(id).subscribe(o => this.order.set(o));
    switch (name) {
      case 'submit': this.service.submitOrder(id).subscribe(reload); break;
      case 'transferMaterials': this.createRmTransfer(id); break;
      case 'receiveFinishedGoods': this.openReceiptDialog(); break;
      case 'close': this.service.closeOrder(id).subscribe(reload); break;
      case 'reopen': this.service.reopenOrder(id).subscribe(reload); break;
      case 'cancel': this.confirmation.warn('CancelConfirmationMessage', 'Confirm').subscribe(s => {
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
}
