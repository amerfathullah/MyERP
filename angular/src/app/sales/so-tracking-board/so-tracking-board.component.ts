import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import type { SalesOrderTrackingBoardDto } from '../../proxy/sales/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  standalone: true,
  selector: 'app-so-tracking-board',
  imports: [CommonModule, RouterLink, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4 class="mb-0">{{ '::SalesOrderTrackingBoard' | abpLocalization }}</h4>
        <a routerLink="/sales/orders/new" class="btn btn-sm btn-primary">
          <i class="fa fa-plus me-1"></i>{{ '::NewSalesOrder' | abpLocalization }}
        </a>
      </div>

      @if (board(); as b) {
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <div class="card border-0 bg-light">
              <div class="card-body py-2 text-center">
                <small class="text-muted">{{ '::TotalOrders' | abpLocalization }}</small>
                <div class="fw-bold fs-4">{{ b.totalValue ?? 0 | number:'1.0-0' }}</div>
                <small class="text-muted">{{ '::TotalValue' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        </div>

        <div class="row g-2">
          <!-- Ordered -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-warning bg-opacity-10 py-2">
                <span class="fw-bold text-warning">{{ '::Ordered' | abpLocalization }}</span>
                <span class="badge bg-warning ms-1">{{ b.ordered?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of b.ordered ?? []; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                } @empty {
                  <p class="text-muted text-center small">{{ '::NoActiveOrders' | abpLocalization }}</p>
                }
              </div>
            </div>
          </div>

          <!-- Partially Delivered -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-info bg-opacity-10 py-2">
                <span class="fw-bold text-info">{{ '::PartiallyDelivered' | abpLocalization }}</span>
                <span class="badge bg-info ms-1">{{ b.partiallyDelivered?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of b.partiallyDelivered ?? []; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                } @empty {
                  <p class="text-muted text-center small">{{ '::NoActiveOrders' | abpLocalization }}</p>
                }
              </div>
            </div>
          </div>

          <!-- Fully Delivered -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-primary bg-opacity-10 py-2">
                <span class="fw-bold text-primary">{{ '::FullyDelivered' | abpLocalization }}</span>
                <span class="badge bg-primary ms-1">{{ b.fullyDelivered?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of b.fullyDelivered ?? []; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                } @empty {
                  <p class="text-muted text-center small">{{ '::NoActiveOrders' | abpLocalization }}</p>
                }
              </div>
            </div>
          </div>

          <!-- Completed -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-success bg-opacity-10 py-2">
                <span class="fw-bold text-success">{{ '::Completed' | abpLocalization }}</span>
                <span class="badge bg-success ms-1">{{ b.completed?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of b.completed ?? []; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                } @empty {
                  <p class="text-muted text-center small">{{ '::NoActiveOrders' | abpLocalization }}</p>
                }
              </div>
            </div>
          </div>
        </div>
      } @else if (loading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin"></i></div>
      }
    </div>

    <ng-template #orderCard let-card>
      <div class="card mb-2 shadow-sm" [class.border-start-danger]="card.isOverdue" [style.border-left-width]="card.isOverdue ? '3px' : ''">
        <div class="card-body p-2">
          <div class="d-flex justify-content-between align-items-start">
            <a [routerLink]="['/sales/orders', card.orderId]" class="fw-bold text-decoration-none small">
              {{ card.orderNumber }}
            </a>
            @if (card.isOverdue) {
              <span class="badge bg-danger">{{ card.daysOverdue }}d</span>
            }
          </div>
          <div class="small text-muted">{{ card.customerName }}</div>
          <div class="d-flex justify-content-between align-items-center mt-1">
            <span class="small fw-bold">{{ card.grandTotal | number:'1.0-0' }}</span>
            <span class="badge bg-secondary bg-opacity-25 text-dark">{{ card.itemCount }} {{ '::Items' | abpLocalization }}</span>
          </div>
          <div class="progress mt-1" style="height: 5px;">
            <div class="progress-bar bg-info" [style.width.%]="card.perDelivered"></div>
          </div>
          <div class="d-flex justify-content-between mt-1">
            <small class="text-muted">{{ card.expectedDeliveryDate | date:'dd MMM' }}</small>
            <small class="text-muted">{{ card.perDelivered | number:'1.0-0' }}%</small>
          </div>
        </div>
      </div>
    </ng-template>
  `,
})
export class SoTrackingBoardComponent implements OnInit {
  private service = inject(SalesOrderService);
  private companyContext = inject(CompanyContextService);

  board = signal<SalesOrderTrackingBoardDto | null>(null);
  loading = signal(false);

  ngOnInit() {
    this.loadBoard();
  }

  loadBoard() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.loading.set(true);
    this.service.getTrackingBoard(companyId).subscribe({
      next: (data) => {
        this.board.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
