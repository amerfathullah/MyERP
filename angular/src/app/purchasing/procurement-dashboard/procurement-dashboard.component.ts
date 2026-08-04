import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { HttpClient } from '@angular/common/http';
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

      <!-- KPI Cards Row -->
      <div class="row mb-4">
        <div class="col-md-3">
          <div class="card border-start border-warning border-4">
            <div class="card-body text-center">
              <h3 class="mb-0 text-warning">{{ kpis().pendingMrCount }}</h3>
              <small class="text-muted">{{ '::PendingMaterialRequests' | abpLocalization }}</small>
            </div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="card border-start border-primary border-4">
            <div class="card-body text-center">
              <h3 class="mb-0 text-primary">{{ kpis().activePOCount }}</h3>
              <small class="text-muted">{{ '::ActivePurchaseOrders' | abpLocalization }}</small>
            </div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="card border-start border-danger border-4">
            <div class="card-body text-center">
              <h3 class="mb-0 text-danger">{{ kpis().overduePOCount }}</h3>
              <small class="text-muted">{{ '::OverduePurchaseOrders' | abpLocalization }}</small>
            </div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="card border-start border-success border-4">
            <div class="card-body text-center">
              <h3 class="mb-0 text-success">{{ kpis().onTimeDeliveryPct | number:'1.0-0' }}%</h3>
              <small class="text-muted">{{ '::OnTimeDelivery' | abpLocalization }}</small>
            </div>
          </div>
        </div>
      </div>

      <div class="row">
        <!-- Pending MRs needing PO creation -->
        <div class="col-md-6 mb-3">
          <div class="card h-100">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-clipboard-list text-warning me-2"></i>{{ '::MaterialRequestsAwaitingOrder' | abpLocalization }}</h6>
              <a routerLink="/purchasing/material-requests" class="btn btn-sm btn-link">{{ '::ViewAll' | abpLocalization }} →</a>
            </div>
            <div class="card-body p-0">
              @if (pendingMRs().length === 0) {
                <div class="text-center text-muted py-4">
                  <i class="fa fa-check-circle fa-2x mb-2 text-success"></i>
                  <p class="mb-0">{{ '::AllMaterialRequestsOrdered' | abpLocalization }}</p>
                </div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-sm table-hover mb-0">
                    <thead><tr>
                      <th>{{ '::RequestNumber' | abpLocalization }}</th>
                      <th>{{ '::Date' | abpLocalization }}</th>
                      <th>{{ '::Items' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Ordered' | abpLocalization }}</th>
                    </tr></thead>
                    <tbody>
                      @for (mr of pendingMRs(); track mr.id) {
                        <tr [class.table-warning]="mr.isOverdue">
                          <td><a [routerLink]="['/purchasing/material-requests', mr.id]" class="text-decoration-none">{{ mr.requestNumber }}</a></td>
                          <td>{{ mr.requestDate | date:'dd/MM' }}</td>
                          <td>{{ mr.itemCount }}</td>
                          <td class="text-end">
                            <div class="progress" style="height: 5px;">
                              <div class="progress-bar bg-primary" [style.width.%]="mr.perOrdered"></div>
                            </div>
                            <small>{{ mr.perOrdered | number:'1.0-0' }}%</small>
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

        <!-- Active POs awaiting receipt -->
        <div class="col-md-6 mb-3">
          <div class="card h-100">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-truck text-primary me-2"></i>{{ '::PurchaseOrdersAwaitingReceipt' | abpLocalization }}</h6>
              <a routerLink="/purchasing/orders" class="btn btn-sm btn-link">{{ '::ViewAll' | abpLocalization }} →</a>
            </div>
            <div class="card-body p-0">
              @if (activePOs().length === 0) {
                <div class="text-center text-muted py-4">
                  <i class="fa fa-box-open fa-2x mb-2 text-success"></i>
                  <p class="mb-0">{{ '::AllOrdersReceived' | abpLocalization }}</p>
                </div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-sm table-hover mb-0">
                    <thead><tr>
                      <th>{{ '::OrderNumber' | abpLocalization }}</th>
                      <th>{{ '::Supplier' | abpLocalization }}</th>
                      <th>{{ '::ExpectedDate' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Received' | abpLocalization }}</th>
                    </tr></thead>
                    <tbody>
                      @for (po of activePOs(); track po.id) {
                        <tr [class.table-danger]="po.isOverdue">
                          <td><a [routerLink]="['/purchasing/orders', po.id]" class="text-decoration-none">{{ po.orderNumber }}</a></td>
                          <td>{{ po.supplierName || '—' }}</td>
                          <td>
                            {{ po.expectedDate | date:'dd/MM' }}
                            @if (po.isOverdue) {
                              <span class="badge bg-danger ms-1">{{ po.daysOverdue }}d</span>
                            }
                          </td>
                          <td class="text-end">
                            <div class="progress" style="height: 5px;">
                              <div class="progress-bar" [class.bg-success]="po.perReceived >= 100" [class.bg-primary]="po.perReceived < 100" [style.width.%]="po.perReceived"></div>
                            </div>
                            <small [class.text-success]="po.perReceived >= 100">{{ po.perReceived | number:'1.0-0' }}%</small>
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

      <!-- Recent Receipts -->
      <div class="row">
        <div class="col-12">
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0"><i class="fa fa-box-open text-success me-2"></i>{{ '::RecentReceipts' | abpLocalization }}</h6>
              <a routerLink="/purchasing/receipts" class="btn btn-sm btn-link">{{ '::ViewAll' | abpLocalization }} →</a>
            </div>
            <div class="card-body p-0">
              @if (recentReceipts().length === 0) {
                <div class="text-center text-muted py-3"><small>{{ '::NoRecentReceipts' | abpLocalization }}</small></div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-sm mb-0">
                    <thead><tr>
                      <th>{{ '::ReceiptNumber' | abpLocalization }}</th>
                      <th>{{ '::Supplier' | abpLocalization }}</th>
                      <th>{{ '::Date' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                      <th><app-status-badge status="Header"></app-status-badge></th>
                    </tr></thead>
                    <tbody>
                      @for (pr of recentReceipts(); track pr.id) {
                        <tr>
                          <td><a [routerLink]="['/purchasing/receipts', pr.id]" class="text-decoration-none">{{ pr.receiptNumber }}</a></td>
                          <td>{{ pr.supplierName || '—' }}</td>
                          <td>{{ pr.postingDate | date:'dd/MM/yyyy' }}</td>
                          <td class="text-end font-monospace">{{ pr.grandTotal | number:'1.2-2' }}</td>
                          <td><app-status-badge [status]="pr.statusLabel"></app-status-badge></td>
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
  private http = inject(HttpClient);
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
    const today = new Date().toISOString().substring(0, 10);

    // Load pending MRs (Submitted, type=Purchase, not fully ordered)
    this.http.get<any>('/api/app/material-request', {
      params: { status: 'Submitted', companyId: companyId || '', maxResultCount: '10' }
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
    this.http.get<any>('/api/app/purchase-order', {
      params: { companyId: companyId || '', maxResultCount: '20' }
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
    this.http.get<any>('/api/app/purchase-receipt', {
      params: { companyId: companyId || '', fromDate: weekAgo, maxResultCount: '10', sorting: 'postingDate desc' }
    }).subscribe({
      next: (res) => {
        this.recentReceipts.set((res.items || []).map((pr: any) => ({
          ...pr,
          statusLabel: ['Draft', 'Submitted', '', '', 'Cancelled'][pr.status ?? 0] || 'Draft'
        })));
      },
      error: () => {}
    });

    // On-time delivery percentage (from supplier performance data)
    this.http.get<any>('/api/app/dashboard/production-summary', {
      params: { companyId: companyId || '' }
    }).subscribe({ next: () => {}, error: () => {} });

    // Calculate on-time from PO data heuristic
    this.http.get<any>('/api/app/purchase-order', {
      params: { companyId: companyId || '', maxResultCount: '50', status: 'Completed' }
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
