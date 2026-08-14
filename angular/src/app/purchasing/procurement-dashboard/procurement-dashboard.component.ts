import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-procurement-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <div class="container-fluid">
      <div class="d-flex justify-content-between align-items-center mb-3">
        <h4 class="mb-0"><i class="fa fa-shopping-cart text-primary me-2"></i>{{ '::ProcurementDashboard' | abpLocalization }}</h4>
        <div class="btn-group">
          <a routerLink="/purchasing/material-requests/new" class="btn btn-sm btn-outline-primary">
            <i class="fa fa-plus me-1"></i>{{ '::NewMaterialRequest' | abpLocalization }}
          </a>
          <a routerLink="/purchasing/orders/new" class="btn btn-sm btn-outline-success">
            <i class="fa fa-plus me-1"></i>{{ '::NewPurchaseOrder' | abpLocalization }}
          </a>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="row g-3 mb-4">
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm text-center p-3">
            <div class="text-muted small mb-1">{{ '::PendingMaterialRequests' | abpLocalization }}</div>
            <div class="fs-3 fw-bold text-primary">{{ kpis().pendingMrCount }}</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm text-center p-3">
            <div class="text-muted small mb-1">{{ '::ActivePurchaseOrders' | abpLocalization }}</div>
            <div class="fs-3 fw-bold text-info">{{ kpis().activePOCount }}</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm text-center p-3">
            <div class="text-muted small mb-1">{{ '::OverduePurchaseOrders' | abpLocalization }}</div>
            <div class="fs-3 fw-bold text-danger">{{ kpis().overduePOCount }}</div>
          </div>
        </div>
        <div class="col-6 col-md-3">
          <div class="card border-0 shadow-sm text-center p-3">
            <div class="text-muted small mb-1">{{ '::OnTimeDeliveryRate' | abpLocalization }}</div>
            <div class="fs-3 fw-bold text-success">{{ kpis().onTimeDeliveryPct | number:'1.0-0' }}%</div>
          </div>
        </div>
      </div>

      <div class="row g-3">
        <!-- Pending Material Requests -->
        <div class="col-md-6">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-header bg-transparent d-flex justify-content-between align-items-center">
              <span class="fw-bold"><i class="fa fa-clipboard-list text-primary me-2"></i>{{ '::PendingMaterialRequests' | abpLocalization }}</span>
              <a routerLink="/purchasing/material-requests" class="btn btn-sm btn-link p-0 text-decoration-none">{{ '::ViewAll' | abpLocalization }}</a>
            </div>
            <div class="card-body p-0">
              @if (pendingMRs().length === 0) {
                <div class="text-center text-muted py-4 small">{{ '::NoPendingMRs' | abpLocalization }}</div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-hover table-sm mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::Number' | abpLocalization }}</th>
                        <th>{{ '::RequiredBy' | abpLocalization }}</th>
                        <th class="text-center">{{ '::Items' | abpLocalization }}</th>
                        <th class="text-center">{{ '::Ordered' | abpLocalization }}</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (mr of pendingMRs(); track mr.id) {
                        <tr [class.table-warning]="mr.isOverdue">
                          <td>
                            <a [routerLink]="['/purchasing/material-requests', mr.id]" class="fw-semibold text-decoration-none small">
                              {{ mr.requestNumber }}
                            </a>
                            @if (mr.isOverdue) {
                              <span class="badge bg-danger ms-1 small">{{ '::Overdue' | abpLocalization }}</span>
                            }
                          </td>
                          <td class="small">{{ mr.requiredByDate | date:'shortDate' }}</td>
                          <td class="text-center small">{{ mr.itemCount }}</td>
                          <td class="text-center">
                            <div class="progress" style="height: 6px;" [title]="(mr.perOrdered || 0) + '%'">
                              <div class="progress-bar" [style.width.%]="mr.perOrdered || 0"></div>
                            </div>
                          </td>
                          <td class="text-end">
                            <a [routerLink]="['/purchasing/orders/new']" [queryParams]="{ fromMr: mr.id }" class="btn btn-xs btn-outline-primary py-0 px-1" style="font-size: 0.75rem;">
                              <i class="fa fa-shopping-bag me-1"></i>{{ '::CreatePO' | abpLocalization }}
                            </a>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Active Purchase Orders -->
        <div class="col-md-6">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-header bg-transparent d-flex justify-content-between align-items-center">
              <span class="fw-bold"><i class="fa fa-truck-loading text-info me-2"></i>{{ '::ActivePurchaseOrders' | abpLocalization }}</span>
              <a routerLink="/purchasing/orders" class="btn btn-sm btn-link p-0 text-decoration-none">{{ '::ViewAll' | abpLocalization }}</a>
            </div>
            <div class="card-body p-0">
              @if (activePOs().length === 0) {
                <div class="text-center text-muted py-4 small">{{ '::NoActivePOs' | abpLocalization }}</div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-hover table-sm mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::Number' | abpLocalization }}</th>
                        <th>{{ '::Supplier' | abpLocalization }}</th>
                        <th>{{ '::ExpectedDate' | abpLocalization }}</th>
                        <th class="text-center">{{ '::Received' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (po of activePOs(); track po.id) {
                        <tr [class.table-danger]="po.isOverdue">
                          <td>
                            <a [routerLink]="['/purchasing/orders', po.id]" class="fw-semibold text-decoration-none small">
                              {{ po.orderNumber }}
                            </a>
                          </td>
                          <td class="small text-truncate" style="max-width: 130px;">{{ po.supplierName }}</td>
                          <td class="small">
                            {{ po.expectedDate | date:'shortDate' }}
                            @if (po.isOverdue) {
                              <span class="badge bg-danger ms-1 small">{{ po.daysOverdue }}d</span>
                            }
                          </td>
                          <td class="text-center">
                            <div class="progress" style="height: 6px;" [title]="(po.perReceived || 0) + '%'">
                              <div class="progress-bar bg-success" [style.width.%]="po.perReceived || 0"></div>
                            </div>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Recent Purchase Receipts -->
        <div class="col-12">
          <div class="card border-0 shadow-sm">
            <div class="card-header bg-transparent d-flex justify-content-between align-items-center">
              <span class="fw-bold"><i class="fa fa-boxes text-success me-2"></i>{{ '::RecentPurchaseReceipts' | abpLocalization }}</span>
              <a routerLink="/purchasing/receipts" class="btn btn-sm btn-link p-0 text-decoration-none">{{ '::ViewAll' | abpLocalization }}</a>
            </div>
            <div class="card-body p-0">
              @if (recentReceipts().length === 0) {
                <div class="text-center text-muted py-4 small">{{ '::NoRecentReceipts' | abpLocalization }}</div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-hover table-sm mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::ReceiptNumber' | abpLocalization }}</th>
                        <th>{{ '::Supplier' | abpLocalization }}</th>
                        <th>{{ '::PostingDate' | abpLocalization }}</th>
                        <th class="text-end">{{ '::TotalAmount' | abpLocalization }}</th>
                        <th class="text-center">{{ '::Status' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (pr of recentReceipts(); track pr.id) {
                        <tr>
                          <td>
                            <a [routerLink]="['/purchasing/receipts', pr.id]" class="fw-semibold text-decoration-none small">
                              {{ pr.receiptNumber }}
                            </a>
                          </td>
                          <td class="small">{{ pr.supplierName }}</td>
                          <td class="small">{{ pr.postingDate | date:'shortDate' }}</td>
                          <td class="text-end small fw-semibold">{{ pr.grandTotal | number:'1.2-2' }}</td>
                          <td class="text-center">
                            <app-status-badge [status]="pr.statusLabel" />
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ProcurementDashboardComponent implements OnInit {
  private materialRequestService = inject(MaterialRequestService);
  private purchaseOrderService = inject(PurchaseOrderService);
  private purchaseReceiptService = inject(PurchaseReceiptService);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);

  kpis = signal({ pendingMrCount: 0, activePOCount: 0, overduePOCount: 0, onTimeDeliveryPct: 0 });
  pendingMRs = signal<any[]>([]);
  activePOs = signal<any[]>([]);
  recentReceipts = signal<any[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    const companyId = this.companyContext.currentCompanyId();

    // Load pending MRs (Submitted, type=Purchase, not fully ordered)
    this.materialRequestService.getList({
      status: 'Submitted',
      companyId: companyId || undefined,
      maxResultCount: 10,
      skipCount: 0,
    }).subscribe({
      next: (res) => {
        const items = (res.items || [])
          .filter((mr: any) => (mr.perOrdered ?? 0) < 100)
          .map((mr: any) => ({
            ...mr,
            isOverdue: mr.requiredByDate && new Date(mr.requiredByDate) < new Date(),
            itemCount: mr.items?.length || 0,
          }));
        this.pendingMRs.set(items.slice(0, 8));
        this.kpis.update(k => ({ ...k, pendingMrCount: items.length }));
      },
      error: () => {}
    });

    // Load active POs (not fully received)
    this.purchaseOrderService.getList({
      companyId: companyId || undefined,
      maxResultCount: 20,
      skipCount: 0,
    }).subscribe({
      next: (res) => {
        const active = (res.items || [])
          .filter((po: any) => po.status >= 4 && po.status <= 7 && (po.perReceived ?? 0) < 100);

        const withOverdue = active.map((po: any) => {
          const expectedDate = po.supplierPromisedDate || po.expectedDeliveryDate;
          const isOverdue = expectedDate && new Date(expectedDate) < new Date();
          const daysOverdue = isOverdue ? Math.ceil((Date.now() - new Date(expectedDate).getTime()) / 86400000) : 0;
          return { ...po, expectedDate, isOverdue, daysOverdue };
        });

        const overdue = withOverdue.filter((po: any) => po.isOverdue);
        this.activePOs.set(withOverdue.slice(0, 10));
        this.kpis.update(k => ({ ...k, activePOCount: active.length, overduePOCount: overdue.length }));
      },
      error: () => {}
    });

    // Load recent receipts (last 7 days)
    const weekAgo = new Date(Date.now() - 7 * 86400000).toISOString().substring(0, 10);
    this.purchaseReceiptService.getList({
      companyId: companyId || undefined,
      fromDate: weekAgo,
      maxResultCount: 10,
      skipCount: 0,
      sorting: 'postingDate desc',
    }).subscribe({
      next: (res) => {
        this.recentReceipts.set((res.items || []).map((pr: any) => ({
          ...pr,
          statusLabel: ['Draft', 'Submitted', '', '', 'Cancelled'][pr.status ?? 0] || 'Draft'
        })));
      },
      error: () => {}
    });

    // Calculate on-time from PO data heuristic
    this.purchaseOrderService.getList({
      companyId: companyId || undefined,
      maxResultCount: 50,
      skipCount: 0,
      status: 'Completed',
    }).subscribe({
      next: (res) => {
        const completed = (res.items || []);
        if (completed.length > 0) {
          const onTime = completed.filter((po: any) => {
            const expected = po.supplierPromisedDate || po.expectedDeliveryDate;
            if (!expected) return true;
            return (po.perReceived ?? 0) >= 100;
          }).length;
          this.kpis.update(k => ({ ...k, onTimeDeliveryPct: (onTime / completed.length) * 100 }));
        }
      },
      error: () => {}
    });
  }
}
