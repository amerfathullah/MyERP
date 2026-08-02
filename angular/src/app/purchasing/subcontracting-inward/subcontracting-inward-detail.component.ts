import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SubcontractingService } from '../../proxy/purchasing/subcontracting.service';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { VoucherLedgerComponent } from '../../shared/components/voucher-ledger/voucher-ledger.component';
import { SubcontractingInwardOrderService } from '../../proxy/purchasing/subcontracting-inward-order.service';

@Component({
  selector: 'app-subcontracting-inward-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, VoucherLedgerComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="order()?.orderNumber || ('SubcontractingInwardOrder' | abpLocalization)">
      @if (isLoading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (order(); as o) {
        <!-- KPI Cards -->
        <div class="row mb-3">
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
              <span class="badge mt-1" [class]="getStatusClass(o.status)">{{ getStatusName(o.status) }}</span>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'GrandTotal' | abpLocalization }}</small>
              <h4 class="mb-0">{{ o.grandTotal | number:'1.2-2' }}</h4>
              <small class="text-muted">{{ o.currencyCode }}</small>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'PerReceived' | abpLocalization }}</small>
              <div class="progress mt-1" style="height: 20px">
                <div class="progress-bar" [class]="o.perReceived >= 100 ? 'bg-success' : 'bg-info'"
                  [style.width.%]="o.perReceived || 0">{{ o.perReceived | number:'1.0-0' }}%</div>
              </div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'PerBilled' | abpLocalization }}</small>
              <div class="progress mt-1" style="height: 20px">
                <div class="progress-bar bg-warning text-dark" [style.width.%]="o.perBilled || 0">
                  {{ o.perBilled | number:'1.0-0' }}%</div>
              </div>
            </div></div>
          </div>
        </div>

        <!-- Order Details -->
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'OrderDetails' | abpLocalization }}</h6>
          <div class="row">
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'OrderDate' | abpLocalization }}</td><td>{{ o.orderDate | date:'dd/MM/yyyy' }}</td></tr>
                <tr><td class="text-muted">{{ 'Supplier' | abpLocalization }}</td><td><strong>{{ supplierName() || '—' }}</strong></td></tr>
              </table>
            </div>
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                @if (o.salesOrderId) {
                  <tr><td class="text-muted" style="width:40%">{{ 'SalesOrder' | abpLocalization }}</td>
                    <td><a [routerLink]="['/sales/orders', o.salesOrderId]">{{ soNumber() || o.salesOrderId }}</a></td></tr>
                }
                @if (o.subcontractingOrderId) {
                  <tr><td class="text-muted">{{ 'SubcontractingOrder' | abpLocalization }}</td>
                    <td><a [routerLink]="['/purchasing/subcontracting', o.subcontractingOrderId]">{{ scoNumber() || o.subcontractingOrderId }}</a></td></tr>
                }
              </table>
            </div>
          </div>
        </div></div>

        <!-- Items Table -->
        @if (o.items?.length) {
          <div class="card mb-3"><div class="card-body">
            <h6>{{ 'Items' | abpLocalization }} ({{ o.items.length }})</h6>
            <table class="table table-hover table-sm mb-0">
              <thead><tr>
                <th>#</th>
                <th>{{ 'Item' | abpLocalization }}</th>
                <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                <th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                <th class="text-end">{{ 'Received' | abpLocalization }}</th>
                <th class="text-end">{{ 'Pending' | abpLocalization }}</th>
                <th class="text-end">{{ 'Billed' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (item of o.items; track item.id; let i = $index) {
                  <tr>
                    <td>{{ i + 1 }}</td>
                    <td>{{ getItemName(item.itemId) }}</td>
                    <td class="text-end">{{ item.quantity | number:'1.0-2' }}</td>
                    <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ item.amount | number:'1.2-2' }}</td>
                    <td class="text-end">
                      <span [class]="item.receivedQty >= item.quantity ? 'text-success fw-bold' : ''">
                        {{ item.receivedQty | number:'1.0-2' }}
                      </span>
                    </td>
                    <td class="text-end">
                      <span [class]="item.pendingReceiptQty > 0 ? 'text-warning' : 'text-success'">
                        {{ item.pendingReceiptQty | number:'1.0-2' }}
                      </span>
                    </td>
                    <td class="text-end">{{ item.billedQty | number:'1.0-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot><tr class="fw-bold">
                <td colspan="4" class="text-end">{{ 'Total' | abpLocalization }}</td>
                <td class="text-end">{{ o.grandTotal | number:'1.2-2' }}</td>
                <td colspan="3"></td>
              </tr></tfoot>
            </table>
          </div></div>
        }

        <!-- Workflow Actions -->
        <div class="d-flex gap-2 mb-3">
          @if (o.status === 0) {
            <button class="btn btn-primary btn-sm" (click)="submit()">
              <i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}
            </button>
          }
          @if (o.status === 1 || o.status === 2) {
            <button class="btn btn-success btn-sm" (click)="openReceiptDialog()">
              <i class="fa fa-box-open me-1"></i>{{ '::CreateReceipt' | abpLocalization }}
            </button>
            <button class="btn btn-outline-warning btn-sm" (click)="close()">
              <i class="fa fa-lock me-1"></i>{{ 'Close' | abpLocalization }}
            </button>
          }
          @if (o.status !== 4 && o.status !== 5) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancelOrder()">
              <i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}
            </button>
          }
        </div>

        <!-- Receipt Items Dialog -->
        @if (showReceiptDialog()) {
          <div class="card border-success mb-3">
            <div class="card-header bg-success bg-opacity-10 d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-box-open me-2"></i>{{ '::ReceiveItems' | abpLocalization }}</h6>
              <button type="button" class="btn-close btn-sm" (click)="showReceiptDialog.set(false)"></button>
            </div>
            <div class="card-body">
              <p class="text-muted small mb-2">{{ '::ReceiveItemsHelp' | abpLocalization }}</p>
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Ordered' | abpLocalization }}</th>
                    <th class="text-end">{{ '::AlreadyReceived' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Pending' | abpLocalization }}</th>
                    <th class="text-center" style="width: 120px">{{ '::ReceiveQty' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of receiptItems(); track item.itemId) {
                    <tr>
                      <td>{{ getItemName(item.itemId) }}</td>
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
                <button class="btn btn-sm btn-success" (click)="createReceipt()" [disabled]="isCreatingReceipt() || !hasReceivableQty()">
                  @if (isCreatingReceipt()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                  <i class="fa fa-check me-1"></i>{{ '::CreateReceipt' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <!-- Activity Log -->
        <app-activity-log documentType="SubcontractingInwardOrder" [documentId]="orderId" />
        <app-voucher-ledger voucherType="SubcontractingInwardOrder" [voucherId]="orderId" [showStock]="true" [showAccounting]="true" />
      }
    </abp-page>
  `
})
export class SubcontractingInwardDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private confirmation = inject(ConfirmationService);
  private scioService = inject(SubcontractingInwardOrderService);
  private subcontractingService = inject(SubcontractingService);
  private toaster = inject(ToasterService);

  private salesOrderProxyService = inject(SalesOrderService);
  private supplierService = inject(SupplierService);
  private itemService = inject(ItemService);

  order = signal<any>(null);
  isLoading = signal(true);
  orderId = '';
  supplierName = signal('');
  soNumber = signal('');
  scoNumber = signal('');
  private itemNames = signal<Record<string, string>>({});

  // Receipt dialog state
  showReceiptDialog = signal(false);
  receiptItems = signal<{ itemId: string; orderedQty: number; receivedQty: number; pendingQty: number; receiveQty: number }[]>([]);
  isCreatingReceipt = signal(false);

  private statusNames = ['Draft', 'Open', 'Partially Received', 'Completed', 'Closed', 'Cancelled'];
  private statusClasses = ['bg-secondary', 'bg-primary', 'bg-info', 'bg-success', 'bg-dark', 'bg-danger'];

  ngOnInit() {
    this.orderId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadOrder();
  }

  loadOrder() {
    this.isLoading.set(true);
    this.scioService.get(this.orderId).subscribe({
      next: o => {
        this.order.set(o);
        this.isLoading.set(false);
        this.resolveNames(o);
      },
      error: () => { this.isLoading.set(false); }
    });
  }

  getItemName(id: string): string {
    return this.itemNames()[id] || id || '—';
  }

  private resolveNames(o: any) {
    if (o.supplierId) {
      this.supplierService.get(o.supplierId).subscribe({
        next: (s: any) => this.supplierName.set(s.name || s.supplierName || ''),
        error: () => {}
      });
    }
    if (o.salesOrderId) {
      this.salesOrderProxyService.get(o.salesOrderId).subscribe({
        next: (so: any) => this.soNumber.set(so.orderNumber || ''),
        error: () => {}
      });
    }
    if (o.subcontractingOrderId) {
      this.subcontractingService.getOrder(o.subcontractingOrderId).subscribe({
        next: (sco: any) => this.scoNumber.set(sco.orderNumber || ''),
        error: () => {}
      });
    }
    // Resolve item names
    const itemIds = (o.items || []).map((i: any) => i.itemId).filter((id: string) => !!id);
    if (itemIds.length) {
      this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
        next: res => {
          const map: Record<string, string> = {};
          (res.items || []).forEach((item: any) => map[item.id] = item.itemName || item.itemCode || item.id);
          this.itemNames.set(map);
        },
        error: () => {}
      });
    }
  }

  getStatusName(s: number) { return this.statusNames[s] ?? 'Unknown'; }
  getStatusClass(s: number) { return this.statusClasses[s] ?? 'bg-secondary'; }

  submit() {
    this.scioService.submit(this.orderId).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.loadOrder(); },
      error: () => {}
    });
  }

  close() {
    this.confirmation.warn('::CloseConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.scioService.close(this.orderId).subscribe({
        next: () => { this.toaster.success('::SuccessfullyClosed'); this.loadOrder(); },
        error: () => {}
      });
    });
  }

  cancelOrder() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.scioService.cancel(this.orderId).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadOrder(); },
        error: () => {}
      });
    });
  }

  openReceiptDialog(): void {
    const o = this.order();
    if (!o?.items?.length) return;
    const items = (o.items as any[]).filter(i => (i.pendingReceiptQty ?? (i.quantity - (i.receivedQty ?? 0))) > 0)
      .map(i => ({
        itemId: i.itemId,
        orderedQty: i.quantity ?? 0,
        receivedQty: i.receivedQty ?? 0,
        pendingQty: i.pendingReceiptQty ?? Math.max(0, (i.quantity ?? 0) - (i.receivedQty ?? 0)),
        receiveQty: i.pendingReceiptQty ?? Math.max(0, (i.quantity ?? 0) - (i.receivedQty ?? 0)),
      }));
    this.receiptItems.set(items);
    this.showReceiptDialog.set(true);
  }

  hasReceivableQty(): boolean {
    return this.receiptItems().some(i => i.receiveQty > 0);
  }

  createReceipt(): void {
    const items = this.receiptItems().filter(i => i.receiveQty > 0);
    if (!items.length) return;
    this.isCreatingReceipt.set(true);
    const o = this.order();
    this.subcontractingService.createReceipt({
      subcontractingOrderId: o.subcontractingOrderId || this.orderId,
      companyId: o.companyId,
      supplierId: o.supplierId,
      postingDate: new Date().toISOString().split('T')[0],
      items: items.map(i => ({
        itemId: i.itemId,
        itemName: this.getItemName(i.itemId),
        qty: i.receiveQty,
        rate: 0,
      })),
    }).subscribe({
      next: (receipt: any) => {
        this.isCreatingReceipt.set(false);
        this.showReceiptDialog.set(false);
        this.toaster.success('::ReceiptCreatedSuccessfully');
        this.loadOrder();
      },
      error: (err: any) => {
        this.isCreatingReceipt.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }
}
