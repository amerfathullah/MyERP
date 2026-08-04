import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { HttpClient } from '@angular/common/http';

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
            <div class="fw-bold text-success fs-5">{{ (board()?.totalValue ?? 0) | number:'1.0-0' }}</div>
            <small class="text-muted">{{ '::TotalValue' | abpLocalization }}</small>
          </div>
        </div>
      </div>

      @if (isLoading()) {
        <div class="text-center py-5"><span class="spinner-border"></span></div>
      } @else if (board()) {
        <!-- Kanban Board -->
        <div class="row g-3">
          <!-- Ordered Column -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-warning bg-opacity-10 border-warning">
                <span class="fw-bold text-warning"><i class="fas fa-clock me-1"></i>{{ '::Ordered' | abpLocalization }}</span>
                <span class="badge bg-warning text-dark ms-2">{{ board()!.ordered.length }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.ordered; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                }
                @if (!board()!.ordered.length) {
                  <div class="text-center text-muted py-3"><small>{{ '::NoOrders' | abpLocalization }}</small></div>
                }
              </div>
            </div>
          </div>

          <!-- Partially Received -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-info bg-opacity-10 border-info">
                <span class="fw-bold text-info"><i class="fas fa-truck me-1"></i>{{ '::PartiallyReceived' | abpLocalization }}</span>
                <span class="badge bg-info ms-2">{{ board()!.partiallyReceived.length }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.partiallyReceived; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                }
                @if (!board()!.partiallyReceived.length) {
                  <div class="text-center text-muted py-3"><small>{{ '::NoOrders' | abpLocalization }}</small></div>
                }
              </div>
            </div>
          </div>

          <!-- Fully Received -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-primary bg-opacity-10 border-primary">
                <span class="fw-bold text-primary"><i class="fas fa-box-open me-1"></i>{{ '::FullyReceived' | abpLocalization }}</span>
                <span class="badge bg-primary ms-2">{{ board()!.fullyReceived.length }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.fullyReceived; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                }
                @if (!board()!.fullyReceived.length) {
                  <div class="text-center text-muted py-3"><small>{{ '::NoOrders' | abpLocalization }}</small></div>
                }
              </div>
            </div>
          </div>

          <!-- Completed -->
          <div class="col-md-3">
            <div class="card h-100">
              <div class="card-header bg-success bg-opacity-10 border-success">
                <span class="fw-bold text-success"><i class="fas fa-check-circle me-1"></i>{{ '::Completed' | abpLocalization }}</span>
                <span class="badge bg-success ms-2">{{ board()!.completed.length }}</span>
              </div>
              <div class="card-body p-2" style="max-height: 70vh; overflow-y: auto;">
                @for (card of board()!.completed; track card.orderId) {
                  <ng-container *ngTemplateOutlet="orderCard; context: { $implicit: card }"></ng-container>
                }
                @if (!board()!.completed.length) {
                  <div class="text-center text-muted py-3"><small>{{ '::NoOrders' | abpLocalization }}</small></div>
                }
              </div>
            </div>
          </div>
        </div>
      } @else {
        <div class="text-center text-muted py-5">
          <i class="fas fa-box fa-3x mb-3"></i>
          <p>{{ '::NoActiveOrders' | abpLocalization }}</p>
        </div>
      }
    </div>

    <!-- Card Template -->
    <ng-template #orderCard let-card>
      <div class="card mb-2 border-start border-3" [class.border-danger]="card.isOverdue" [class.border-secondary]="!card.isOverdue">
        <div class="card-body p-2">
          <div class="d-flex justify-content-between align-items-start">
            <a [routerLink]="['/purchasing/orders', card.orderId]" class="text-decoration-none fw-medium small">
              {{ card.orderNumber }}
            </a>
            @if (card.isOverdue) {
              <span class="badge bg-danger">{{ card.daysOverdue }}d</span>
            }
          </div>
          <div class="small text-muted mt-1">{{ card.supplierName }}</div>
          <div class="d-flex justify-content-between align-items-center mt-1">
            <span class="small fw-medium">{{ card.grandTotal | number:'1.0-0' }}</span>
            <span class="small text-muted">{{ card.itemCount }} {{ '::Items' | abpLocalization }}</span>
          </div>
          @if (card.perReceived > 0 && card.perReceived < 100) {
            <div class="progress mt-1" style="height: 4px;">
              <div class="progress-bar bg-info" [style.width.%]="card.perReceived"></div>
            </div>
          }
          @if (card.expectedDate) {
            <div class="small text-muted mt-1">
              <i class="fas fa-calendar-day me-1"></i>{{ card.expectedDate | date:'dd MMM' }}
            </div>
          }
        </div>
      </div>
    </ng-template>
  `
})
export class PoTrackingBoardComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  board = signal<any>(null);
  isLoading = signal(false);

  ngOnInit() {
    const companyId = this.companyContext.currentCompanyId();
    if (companyId) {
      this.loadBoard(companyId);
    }
  }

  private loadBoard(companyId: string) {
    this.isLoading.set(true);
    this.http.get(`/api/app/purchase-order/tracking-board?companyId=${companyId}`).subscribe({
      next: (data: any) => { this.board.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }
}
