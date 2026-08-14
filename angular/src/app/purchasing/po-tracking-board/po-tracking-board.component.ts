import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import type { PurchaseOrderTrackingBoardDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-po-tracking-board',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <!-- Header + KPIs -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4 class="mb-0"><i class="fas fa-columns me-2"></i>{{ '::PurchaseOrderTrackingBoard' | abpLocalization }}</h4>
        <div class="d-flex gap-3">
          <div class="text-center">
            <div class="fw-bold text-primary fs-5">{{ board()?.totalOrders ?? 0 }}</div>
            <small class="text-muted">{{ '::TotalOrders' | abpLocalization }}</small>
          </div>
          @if (board()?.overdueCount) {
            <div class="text-center">
              <div class="fw-bold text-danger fs-5">{{ board()!.overdueCount }}</div>
              <small class="text-muted">{{ '::Overdue' | abpLocalization }}</small>
            </div>
          }
          <div class="text-center">
            <div class="fw-bold text-success fs-5">{{ board()?.totalValue ?? 0 | number:'1.2-2' }}</div>
            <small class="text-muted">{{ '::TotalValue' | abpLocalization }}</small>
          </div>
        </div>
      </div>

      <!-- Loading -->
      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status"></div>
        </div>
      }

      <!-- Kanban Board -->
      @if (!isLoading() && board()) {
        <div class="row g-3">
          <!-- Ordered -->
          <div class="col-md-3">
            <div class="card bg-light border-0 shadow-sm">
              <div class="card-header bg-transparent border-0 d-flex justify-content-between align-items-center">
                <span class="fw-bold"><i class="fas fa-file-alt text-secondary me-2"></i>{{ '::Ordered' | abpLocalization }}</span>
                <span class="badge bg-secondary rounded-pill">{{ board()!.ordered?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="min-height: 400px; max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.ordered; track card.orderId) {
                  <ng-container *ngTemplateOutlet="cardTemplate; context: { $implicit: card }"></ng-container>
                }
              </div>
            </div>
          </div>

          <!-- Partially Received -->
          <div class="col-md-3">
            <div class="card bg-light border-0 shadow-sm">
              <div class="card-header bg-transparent border-0 d-flex justify-content-between align-items-center">
                <span class="fw-bold"><i class="fas fa-boxes text-warning me-2"></i>{{ '::PartiallyReceived' | abpLocalization }}</span>
                <span class="badge bg-warning rounded-pill">{{ board()!.partiallyReceived?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="min-height: 400px; max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.partiallyReceived; track card.orderId) {
                  <ng-container *ngTemplateOutlet="cardTemplate; context: { $implicit: card }"></ng-container>
                }
              </div>
            </div>
          </div>

          <!-- Fully Received -->
          <div class="col-md-3">
            <div class="card bg-light border-0 shadow-sm">
              <div class="card-header bg-transparent border-0 d-flex justify-content-between align-items-center">
                <span class="fw-bold"><i class="fas fa-truck-loading text-info me-2"></i>{{ '::FullyReceived' | abpLocalization }}</span>
                <span class="badge bg-info rounded-pill">{{ board()!.fullyReceived?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="min-height: 400px; max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.fullyReceived; track card.orderId) {
                  <ng-container *ngTemplateOutlet="cardTemplate; context: { $implicit: card }"></ng-container>
                }
              </div>
            </div>
          </div>

          <!-- Completed / Closed -->
          <div class="col-md-3">
            <div class="card bg-light border-0 shadow-sm">
              <div class="card-header bg-transparent border-0 d-flex justify-content-between align-items-center">
                <span class="fw-bold"><i class="fas fa-check-circle text-success me-2"></i>{{ '::Completed' | abpLocalization }}</span>
                <span class="badge bg-success rounded-pill">{{ board()!.completed?.length ?? 0 }}</span>
              </div>
              <div class="card-body p-2" style="min-height: 400px; max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.completed; track card.orderId) {
                  <ng-container *ngTemplateOutlet="cardTemplate; context: { $implicit: card }"></ng-container>
                }
              </div>
            </div>
          </div>
        </div>
      }
    </div>

    <!-- Reusable Card Template -->
    <ng-template #cardTemplate let-card>
      <div class="card mb-2 border-0 shadow-sm" [class.border-start]="card.isOverdue" [class.border-danger]="card.isOverdue" [class.border-3]="card.isOverdue">
        <div class="card-body p-2">
          <div class="d-flex justify-content-between align-items-start mb-1">
            <a [routerLink]="['/purchasing/orders', card.orderId]" class="fw-bold text-decoration-none small">
              {{ card.orderNumber }}
            </a>
            @if (card.isOverdue) {
              <span class="badge bg-danger ms-1" style="font-size: 0.7rem;">{{ card.daysOverdue }}d {{ '::Overdue' | abpLocalization }}</span>
            }
          </div>
          <div class="small text-truncate text-muted mb-1">{{ card.supplierName }}</div>
          <div class="d-flex justify-content-between small text-muted">
            <span>{{ card.grandTotal | number:'1.2-2' }}</span>
            <span>{{ card.expectedDeliveryDate | date:'shortDate' }}</span>
          </div>
          @if (card.perReceived > 0 && card.perReceived < 100) {
            <div class="progress mt-1" style="height: 4px;">
              <div class="progress-bar bg-warning" [style.width.%]="card.perReceived"></div>
            </div>
          }
        </div>
      </div>
    </ng-template>
  `
})
export class PoTrackingBoardComponent implements OnInit {
  private purchaseOrderService = inject(PurchaseOrderService);
  private companyContext = inject(CompanyContextService);

  board = signal<PurchaseOrderTrackingBoardDto | null>(null);
  isLoading = signal(false);

  ngOnInit() {
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) {
      this.loadBoard(companyId);
    }
  }

  private loadBoard(companyId: string) {
    this.isLoading.set(true);
    this.purchaseOrderService.getTrackingBoard(companyId).subscribe({
      next: (data) => { this.board.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }
}
