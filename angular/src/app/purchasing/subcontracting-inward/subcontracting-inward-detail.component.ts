import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
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
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, VoucherLedgerComponent],
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

        <!-- Activity Log -->
        <app-activity-log documentType="SubcontractingInwardOrder" [documentId]="orderId" />
        <app-voucher-ledger voucherType="SubcontractingInwardOrder" [voucherId]="orderId" [showStock]="true" [showAccounting]="true" />
      }
    </abp-page>
  `
})
export class SubcontractingInwardDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private scioService = inject(SubcontractingInwardOrderService);
  private toaster = inject(ToasterService);

  private http = inject(HttpClient);

  order = signal<any>(null);
  isLoading = signal(true);
  orderId = '';
  supplierName = signal('');
  soNumber = signal('');
  scoNumber = signal('');
  private itemNames = signal<Record<string, string>>({});

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
      this.http.get<any>(`/api/app/supplier/${o.supplierId}`).subscribe({
        next: s => this.supplierName.set(s.name || s.supplierName || ''),
        error: () => {}
      });
    }
    if (o.salesOrderId) {
      this.http.get<any>(`/api/app/sales-order/${o.salesOrderId}`).subscribe({
        next: so => this.soNumber.set(so.orderNumber || ''),
        error: () => {}
      });
    }
    if (o.subcontractingOrderId) {
      this.http.get<any>(`/api/app/subcontracting/${o.subcontractingOrderId}`).subscribe({
        next: sco => this.scoNumber.set(sco.orderNumber || ''),
        error: () => {}
      });
    }
    // Resolve item names
    const itemIds = (o.items || []).map((i: any) => i.itemId).filter((id: string) => !!id);
    if (itemIds.length) {
      this.http.get<any>('/api/app/item', { params: { maxResultCount: '500' } }).subscribe({
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
}
