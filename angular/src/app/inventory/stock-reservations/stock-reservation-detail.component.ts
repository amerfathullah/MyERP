import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { StockReservationService } from '../../proxy/inventory/stock-reservation.service';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-stock-reservation-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="'StockReservationEntry' | abpLocalization">
      @if (isLoading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (entry(); as e) {
        <!-- KPI Cards -->
        <div class="row mb-3">
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
              <span class="badge mt-1" [class]="getStatusClass(e.status)">{{ getStatusName(e.status) }}</span>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'ReservedQty' | abpLocalization }}</small>
              <h4 class="mb-0">{{ e.reservedQty | number:'1.0-2' }}</h4>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'DeliveredQty' | abpLocalization }}</small>
              <h4 class="mb-0">{{ e.deliveredQty | number:'1.0-2' }}</h4>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'AvailableQty' | abpLocalization }}</small>
              <h4 class="mb-0" [class]="(e.availableQty > 0) ? 'text-success' : 'text-danger'">
                {{ e.availableQty | number:'1.0-2' }}
              </h4>
            </div></div>
          </div>
        </div>

        <!-- Fulfillment Progress -->
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'FulfillmentProgress' | abpLocalization }}</h6>
          <div class="progress" style="height: 24px">
            @if (e.reservedQty > 0) {
              <div class="progress-bar bg-success" [style.width.%]="(e.deliveredQty / e.reservedQty * 100)">
                {{ 'Delivered' | abpLocalization }}: {{ e.deliveredQty | number:'1.0-2' }}
              </div>
              <div class="progress-bar bg-info" [style.width.%]="(e.availableQty / e.reservedQty * 100)">
                {{ 'Available' | abpLocalization }}: {{ e.availableQty | number:'1.0-2' }}
              </div>
            }
          </div>
        </div></div>

        <!-- Details Card -->
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'ReservationDetails' | abpLocalization }}</h6>
          <div class="row">
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'Item' | abpLocalization }}</td><td><strong>{{ itemName() || e.itemId || '...' }}</strong></td></tr>
                <tr><td class="text-muted">{{ 'Warehouse' | abpLocalization }}</td><td>{{ warehouseName() || e.warehouseId || '...' }}</td></tr>
              </table>
            </div>
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'VoucherType' | abpLocalization }}</td><td>{{ e.voucherType || '—' }}</td></tr>
                <tr><td class="text-muted">{{ 'VoucherId' | abpLocalization }}</td>
                  <td>
                    @if (e.voucherId && e.voucherType === 'SalesOrder') {
                      <a [routerLink]="['/sales/orders', e.voucherId]">{{ e.voucherNumber || e.voucherId }}</a>
                    } @else {
                      {{ e.voucherNumber || e.voucherId || '—' }}
                    }
                  </td>
                </tr>
                <tr><td class="text-muted">{{ 'Created' | abpLocalization }}</td><td>{{ e.creationTime | date:'dd/MM/yyyy HH:mm' }}</td></tr>
              </table>
            </div>
          </div>
        </div></div>

        <!-- Workflow Actions -->
        <div class="d-flex gap-2 mb-3">
          @if (e.status === 1) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancelEntry()">
              <i class="fa fa-times me-1"></i>{{ 'CancelReservation' | abpLocalization }}
            </button>
          }
        </div>

        <app-activity-log documentType="StockReservationEntry" [documentId]="entryId" />
      }
    </abp-page>
  `
})
export class StockReservationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private sreService = inject(StockReservationService);
  private warehouseService = inject(WarehouseService);
  private itemService = inject(ItemService);
  private toaster = inject(ToasterService);

  entry = signal<any>(null);
  isLoading = signal(true);
  itemName = signal<string>('');
  warehouseName = signal<string>('');
  entryId = '';

  private statusNames = ['Draft', 'Submitted', 'Cancelled'];
  private statusClasses = ['bg-secondary', 'bg-success', 'bg-danger'];

  ngOnInit() {
    this.entryId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadEntry();
  }

  loadEntry() {
    this.isLoading.set(true);
    this.sreService.get(this.entryId).subscribe({
      next: e => {
        this.entry.set(e);
        this.isLoading.set(false);
        // Resolve item + warehouse names
        if (e.itemId) {
          this.itemService.get(e.itemId).subscribe({
            next: item => this.itemName.set(`${item.itemCode} — ${item.itemName}`),
            error: () => {}
          });
        }
        if (e.warehouseId) {
          this.warehouseService.get(e.warehouseId).subscribe({
            next: wh => this.warehouseName.set(wh.name),
            error: () => {}
          });
        }
      },
      error: () => { this.isLoading.set(false); }
    });
  }

  getStatusName(s: number) { return this.statusNames[s] ?? 'Unknown'; }
  getStatusClass(s: number) { return this.statusClasses[s] ?? 'bg-secondary'; }

  cancelEntry() {
    this.confirmation.warn('::CancelReservationConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.sreService.cancel(this.entryId).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadEntry(); },
        error: () => {}
      });
    });
  }
}
