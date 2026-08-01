import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ShipmentService } from '../../proxy/crm/shipment.service';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  standalone: true,
  selector: 'app-shipment-list',
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-truck me-2"></i>{{ 'Shipments' | abpLocalization }}</h5>
          <a routerLink="new" class="btn btn-primary btn-sm">
            <i class="fas fa-plus me-1"></i>{{ 'NewShipment' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm"
                     [placeholder]="'::Search' | abpLocalization" (keyup.enter)="loadData()">
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="loadData()">
                <option value="">{{ 'AllStatuses' | abpLocalization }}</option>
                <option value="Draft">Draft</option>
                <option value="Booked">{{ 'Booked' | abpLocalization }}</option>
                <option value="InTransit">{{ 'InTransit' | abpLocalization }}</option>
                <option value="Delivered">{{ 'Delivered' | abpLocalization }}</option>
                <option value="Cancelled">{{ 'Cancelled' | abpLocalization }}</option>
              </select>
            </div>
          </div>
          @if (shipments().length === 0) {
            <div class="text-center py-4 text-muted">
              <i class="fas fa-truck fa-2x mb-2"></i>
              <p>{{ 'NoShipmentsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'ShipmentNumber' | abpLocalization }}</th>
                  <th>{{ 'Carrier' | abpLocalization }}</th>
                  <th>{{ 'TrackingNumber' | abpLocalization }}</th>
                  <th>{{ 'PickupDate' | abpLocalization }}</th>
                  <th>{{ 'DeliveryNoteCount' | abpLocalization }}</th>
                  <th>{{ 'ValueOfGoods' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (item of shipments(); track item.id) {
                  <tr>
                    <td><a [routerLink]="[item.id]">{{ item.shipmentNumber }}</a></td>
                    <td>{{ item.carrier || '—' }}</td>
                    <td>
                      @if (item.trackingUrl) {
                        <a [href]="item.trackingUrl" target="_blank">{{ item.trackingNumber }}</a>
                      } @else {
                        {{ item.trackingNumber || '—' }}
                      }
                    </td>
                    <td>{{ item.pickupDate ? (item.pickupDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td><span class="badge bg-info">{{ item.deliveryNoteCount }}</span></td>
                    <td>{{ item.valueOfGoods ? (item.valueOfGoods | number:'1.2-2') : '—' }}</td>
                    <td>
                      @switch (item.status) {
                        @case (0) { <span class="badge bg-secondary">Draft</span> }
                        @case (1) { <span class="badge bg-primary">{{ 'Booked' | abpLocalization }}</span> }
                        @case (2) { <span class="badge bg-info">{{ 'InTransit' | abpLocalization }}</span> }
                        @case (3) { <span class="badge bg-success">{{ 'Delivered' | abpLocalization }}</span> }
                        @case (4) { <span class="badge bg-dark">{{ 'Cancelled' | abpLocalization }}</span> }
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (item.status === 0) {
                          <button class="btn btn-outline-primary btn-sm" (click)="submit(item.id)" title="Book">
                            <i class="fas fa-check"></i>
                          </button>
                        }
                        @if (item.status === 1) {
                          <button class="btn btn-outline-info btn-sm" (click)="markInTransit(item.id)">
                            <i class="fas fa-shipping-fast"></i>
                          </button>
                        }
                        @if (item.status === 1 || item.status === 2) {
                          <button class="btn btn-outline-success btn-sm" (click)="markDelivered(item.id)">
                            <i class="fas fa-box-check"></i>
                          </button>
                        }
                        @if (item.status < 3) {
                          <button class="btn btn-outline-danger btn-sm" (click)="cancelShipment(item.id)">
                            <i class="fas fa-times"></i>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage()"
                            (pageChange)="onPageChange($event)"></app-pagination>
          }
        </div>
      </div>
    </div>
  `
})
export class ShipmentListComponent implements OnInit {
  private shipmentService = inject(ShipmentService);
  private confirmation = inject(ConfirmationService);
  shipments = signal<any[]>([]);
  totalCount = signal(0);
  currentPage = signal(0);
  searchTerm = '';
  statusFilter = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    const params: any = { skipCount: this.currentPage() * 10, maxResultCount: 10 };
    if (this.searchTerm) params.filter = this.searchTerm;
    if (this.statusFilter) params.status = this.statusFilter;
    this.shipmentService.getList({ skipCount: this.currentPage() * 10, maxResultCount: 10, sorting: '', filter: this.searchTerm || undefined, status: this.statusFilter || undefined } as any).subscribe({
      next: res => {
        this.shipments.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
      error: () => {}
    });
  }

  submit(id: string) { this.shipmentService.submit(id).subscribe({ next: () => this.loadData(), error: () => {} }); }
  markInTransit(id: string) { this.shipmentService.markInTransit(id).subscribe({ next: () => this.loadData(), error: () => {} }); }
  markDelivered(id: string) { this.shipmentService.markDelivered(id).subscribe({ next: () => this.loadData(), error: () => {} }); }
  cancelShipment(id: string) {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.shipmentService.cancel(id).subscribe({ next: () => this.loadData(), error: () => {} });
    });
  }

  onPageChange(e: PageEvent) { this.currentPage.set(e.pageIndex); this.loadData(); }
}
