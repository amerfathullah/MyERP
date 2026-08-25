import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerService } from '../proxy/sales/customer.service';
import type { CustomerDto } from '../proxy/sales/models';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { PartyPerformanceService } from '../proxy/core/party-performance.service';
import { PartyDashboardService } from '../proxy/accounting/party-dashboard.service';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../shared/components/activity-log/activity-log.component';
import { AddressManagerComponent } from '../shared/components/address-manager/address-manager.component';
import { ContactManagerComponent } from '../shared/components/contact-manager/contact-manager.component';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent, AddressManagerComponent, ContactManagerComponent],
  template: `
    <app-breadcrumb />

    @if (loading()) {
      <div class="d-flex justify-content-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    } @else if (entity(); as customer) {
      <!-- Header -->
      <div class="card mb-4">
        <div class="card-body d-flex align-items-center justify-content-between">
          <div>
            <h3 class="mb-1">
              <i class="fas fa-user-tie me-2 text-primary"></i>{{ customer.name }}
            </h3>
            <span class="badge" [class]="customer.isActive ? 'bg-success' : 'bg-secondary'">
              {{ (customer.isActive ? '::Active' : '::Inactive') | abpLocalization }}
            </span>
            @if (customer.customerCode) {
              <span class="badge bg-light text-dark ms-2">{{ customer.customerCode }}</span>
            }
          </div>
          <a [routerLink]="['/customers', entityId, 'edit']" class="btn btn-outline-primary">
            <i class="fas fa-pencil-alt me-1"></i> {{ '::Edit' | abpLocalization }}
          </a>
        </div>
      </div>

      <!-- Info Grid -->
      <div class="row mb-4">
        <!-- Company Info -->
        <div class="col-md-6 mb-3">
          <div class="card h-100">
            <div class="card-header"><i class="fas fa-building me-2"></i>{{ '::CompanyInfo' | abpLocalization }}</div>
            <div class="card-body">
              <table class="table table-sm table-borderless mb-0">
                <tbody>
                  <tr>
                    <th class="text-muted w-40">{{ '::Name' | abpLocalization }}</th>
                    <td>{{ customer.name }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Code' | abpLocalization }}</th>
                    <td>{{ customer.customerCode || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::TIN' | abpLocalization }}</th>
                    <td class="font-monospace">{{ customer.tin || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::SST' | abpLocalization }}</th>
                    <td class="font-monospace">{{ customer.sstRegistrationNumber || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::BRN' | abpLocalization }}</th>
                    <td class="font-monospace">{{ customer.registrationNumber || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::CreditLimit' | abpLocalization }}</th>
                    <td class="font-monospace fw-semibold">{{ (customer.creditLimit ? (customer.creditLimit | number:'1.2-2') : ('::NoLimit' | abpLocalization)) }}</td>
                  </tr>
                  @if (customer.soRequired) {
                    <tr>
                      <th class="text-muted">{{ '::SalesOrderRequired' | abpLocalization }}</th>
                      <td><span class="badge bg-warning text-dark">{{ '::Yes' | abpLocalization }}</span></td>
                    </tr>
                  }
                  @if (customer.dnRequired) {
                    <tr>
                      <th class="text-muted">{{ '::DeliveryNoteRequired' | abpLocalization }}</th>
                      <td><span class="badge bg-info text-dark">{{ '::Yes' | abpLocalization }}</span></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Contact Info -->
        <div class="col-md-6 mb-3">
          <div class="card h-100">
            <div class="card-header"><i class="fas fa-address-book me-2"></i>{{ '::ContactInfo' | abpLocalization }}</div>
            <div class="card-body">
              <table class="table table-sm table-borderless mb-0">
                <tbody>
                  <tr>
                    <th class="text-muted w-40">{{ '::ContactPerson' | abpLocalization }}</th>
                    <td>{{ customer.contactPerson || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Phone' | abpLocalization }}</th>
                    <td>{{ customer.phone || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Email' | abpLocalization }}</th>
                    <td>
                      @if (customer.email) {
                        <a [href]="'mailto:' + customer.email">{{ customer.email }}</a>
                      } @else { — }
                    </td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Website' | abpLocalization }}</th>
                    <td>
                      @if (customer.website) {
                        <a [href]="customer.website" target="_blank">{{ customer.website }}</a>
                      } @else { — }
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <!-- Address -->
      @if (customer.address || customer.city || customer.state) {
        <div class="card mb-4">
          <div class="card-header"><i class="fas fa-map-marker-alt me-2"></i>{{ '::Address' | abpLocalization }}</div>
          <div class="card-body">
            <p class="mb-0">
              {{ customer.address }}
              @if (customer.city) { <br>{{ customer.city }} }
              @if (customer.state) { , {{ customer.state }} }
              @if (customer.postalCode) { {{ customer.postalCode }} }
              @if (customer.country) { <br>{{ customer.country }} }
            </p>
          </div>
        </div>
      }

      <!-- Financial Summary -->
      <div class="card mb-4">
        <div class="card-header"><i class="fas fa-file-invoice-dollar me-2"></i>{{ '::FinancialSummary' | abpLocalization }}</div>
        <div class="card-body">
          @if (outstandingLoading()) {
            <div class="text-center py-3">
              <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
            </div>
          } @else {
            <div class="row text-center">
              <div class="col-sm-6">
                <div class="fs-4 fw-bold text-primary">{{ outstandingCount() }}</div>
                <div class="text-muted small">{{ '::OutstandingInvoices' | abpLocalization }}</div>
              </div>
              <div class="col-sm-6">
                <div class="fs-4 fw-bold text-danger">{{ outstandingTotal() | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ '::TotalOutstandingAmount' | abpLocalization }}</div>
              </div>
            </div>

            @if (dashboard(); as dash) {
              <hr class="my-3" />
              <div class="row text-center mb-2">
                <div class="col-sm-6">
                  <div class="fs-5 fw-bold text-success">{{ dash.ytdBilling | number:'1.2-2' }}</div>
                  <div class="text-muted small">{{ '::YtdBilling' | abpLocalization }}</div>
                </div>
                <div class="col-sm-6">
                  <div class="fs-5 fw-bold text-warning">{{ dash.loyaltyPoints | number:'1.0-0' }}</div>
                  <div class="text-muted small">{{ '::AvailablePoints' | abpLocalization }}</div>
                </div>
              </div>
              <div class="text-center">
                @if (dash.companies?.length) {
                  <div class="d-flex flex-wrap gap-1 justify-content-center">
                    @for (c of dash.companies; track c.id) {
                      <span class="badge bg-light text-dark border">{{ c.name }}</span>
                    }
                  </div>
                  <div class="text-muted small mt-1">{{ '::TransactedCompanies' | abpLocalization }}</div>
                } @else {
                  <div class="text-muted small">{{ '::NoTransactionsYet' | abpLocalization }}</div>
                }
              </div>
            }

            <!-- Aging Buckets -->
            @if (agingBuckets().length > 0) {
              <hr class="my-3" />
              <h6 class="text-muted mb-2"><i class="fas fa-clock me-1"></i>{{ '::AgingBreakdown' | abpLocalization }}</h6>
              <div class="row g-2">
                @for (bucket of agingBuckets(); track $index) {
                  <div class="col">
                    <div class="text-center p-2 border rounded" [class.border-success]="$index === 0" [class.border-warning]="$index === 1 || $index === 2" [class.border-danger]="$index >= 3">
                      <div class="fw-bold" [class.text-success]="$index === 0" [class.text-warning]="$index === 1 || $index === 2" [class.text-danger]="$index >= 3">
                        {{ bucket.amount | number:'1.0-0' }}
                      </div>
                      <div class="text-muted" style="font-size: 0.7rem">{{ bucket.label }}</div>
                    </div>
                  </div>
                }
              </div>
            }
          }
        </div>
      </div>

      <!-- Performance Metrics -->
      @if (performance(); as perf) {
        <div class="card mb-4">
          <div class="card-header"><i class="fas fa-chart-line me-2"></i>{{ '::PerformanceMetrics' | abpLocalization }}</div>
          <div class="card-body">
            <!-- KPI Row -->
            <div class="row text-center mb-3">
              <div class="col-sm-3">
                <div class="fs-5 fw-bold text-primary">{{ perf.totalRevenue | number:'1.0-0' }}</div>
                <div class="text-muted small">{{ '::TotalRevenue' | abpLocalization }}</div>
              </div>
              <div class="col-sm-3">
                <div class="fs-5 fw-bold">{{ perf.totalOrders }}</div>
                <div class="text-muted small">{{ '::TotalOrders' | abpLocalization }}</div>
              </div>
              <div class="col-sm-3">
                <div class="fs-5 fw-bold">{{ perf.averageOrderValue | number:'1.0-0' }}</div>
                <div class="text-muted small">{{ '::AvgOrderValue' | abpLocalization }}</div>
              </div>
              <div class="col-sm-3">
                <div class="fs-5 fw-bold" [class.text-success]="perf.onTimePaymentPercent >= 80" [class.text-warning]="perf.onTimePaymentPercent >= 50 && perf.onTimePaymentPercent < 80" [class.text-danger]="perf.onTimePaymentPercent < 50">
                  {{ perf.onTimePaymentPercent }}%
                </div>
                <div class="text-muted small">{{ '::OnTimePayment' | abpLocalization }}</div>
              </div>
            </div>

            <!-- Revenue Growth -->
            <div class="row mb-3">
              <div class="col-sm-6">
                <div class="d-flex align-items-center">
                  <span class="text-muted small me-2">{{ '::ThisMonth' | abpLocalization }}:</span>
                  <span class="fw-bold">{{ perf.revenueThisMonth | number:'1.0-0' }}</span>
                  @if (perf.revenueGrowthPercent !== 0) {
                    <span class="badge ms-2" [class.bg-success]="perf.revenueGrowthPercent > 0" [class.bg-danger]="perf.revenueGrowthPercent < 0">
                      <i class="fas" [class.fa-arrow-up]="perf.revenueGrowthPercent > 0" [class.fa-arrow-down]="perf.revenueGrowthPercent < 0"></i>
                      {{ perf.revenueGrowthPercent | number:'1.0-0' }}%
                    </span>
                  }
                </div>
              </div>
              <div class="col-sm-6">
                @if (perf.creditLimit > 0) {
                  <div class="d-flex align-items-center">
                    <span class="text-muted small me-2">{{ '::CreditUsage' | abpLocalization }}:</span>
                    <div class="progress flex-grow-1" style="height: 8px;">
                      <div class="progress-bar" [class.bg-success]="perf.creditUtilizationPercent < 70" [class.bg-warning]="perf.creditUtilizationPercent >= 70 && perf.creditUtilizationPercent < 90" [class.bg-danger]="perf.creditUtilizationPercent >= 90" [style.width.%]="perf.creditUtilizationPercent"></div>
                    </div>
                    <span class="ms-2 small fw-bold">{{ perf.creditUtilizationPercent }}%</span>
                  </div>
                }
              </div>
            </div>

            <!-- Revenue Trend Mini Chart -->
            @if (perf.revenueTrend.length > 0) {
              <h6 class="text-muted mb-2"><i class="fas fa-chart-bar me-1"></i>{{ '::RevenueTrend' | abpLocalization }}</h6>
              <div class="d-flex align-items-end gap-1" style="height: 60px;">
                @for (point of perf.revenueTrend; track $index) {
                  <div class="flex-fill text-center">
                    <div class="bg-primary bg-opacity-75 rounded-top mx-auto" [style.height.%]="getBarHeight(point.amount, perf.revenueTrend)" style="min-height: 2px; width: 80%;"></div>
                    <div class="text-muted" style="font-size: 0.6rem;">{{ point.month }}</div>
                  </div>
                }
              </div>
            }
          </div>
        </div>
      } @else if (performanceLoading()) {
        <div class="card mb-4">
          <div class="card-body text-center py-3">
            <div class="spinner-border spinner-border-sm text-primary"></div>
            <span class="ms-2 text-muted small">{{ '::LoadingPerformanceMetrics' | abpLocalization }}</span>
          </div>
        </div>
      }

      <!-- Quick Actions -->
      <div class="card mb-4">
        <div class="card-body">
          <div class="d-flex flex-wrap gap-2">
            <a [routerLink]="['/sales/orders/new']" [queryParams]="{ customerId: entityId }" class="btn btn-outline-primary">
              <i class="fas fa-file-alt me-1"></i> {{ '::CreateSalesOrder' | abpLocalization }}
            </a>
            <a [routerLink]="['/sales/invoices/new']" [queryParams]="{ customerId: entityId }" class="btn btn-outline-primary">
              <i class="fas fa-file-invoice me-1"></i> {{ '::CreateInvoice' | abpLocalization }}
            </a>
            <a [routerLink]="['/accounting/reports/statement-of-accounts']" [queryParams]="{ partyType: 'Customer', partyId: entityId }" class="btn btn-outline-secondary">
              <i class="fas fa-scroll me-1"></i> {{ '::ViewStatement' | abpLocalization }}
            </a>
          </div>
        </div>
      </div>

      <!-- Addresses & Contacts -->
      <div class="row mb-4">
        <div class="col-md-6">
          <app-address-manager [partyType]="'Customer'" [partyId]="entityId" />
        </div>
        <div class="col-md-6">
          <app-contact-manager [partyType]="'Customer'" [partyId]="entityId" />
        </div>
      </div>

      <!-- Activity Log -->
      <app-activity-log [documentType]="'Customer'" [documentId]="entityId" />
    }
  `,
  styles: [`
    .w-40 { width: 40%; }
  `],
})
export class CustomerDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private customerService = inject(CustomerService);
  private reconciliationService = inject(PaymentReconciliationService);
  private partyPerformanceService = inject(PartyPerformanceService);
  private partyDashboardService = inject(PartyDashboardService);

  entity = signal<CustomerDto | null>(null);
  entityId = '';
  loading = signal(true);
  outstandingLoading = signal(true);
  outstandingCount = signal(0);
  outstandingTotal = signal(0);
  agingBuckets = signal<{ label: string; amount: number }[]>([]);
  performance = signal<any>(null);
  performanceLoading = signal(true);
  dashboard = signal<any>(null);

  ngOnInit() {
    this.entityId = this.route.snapshot.params['id'];
    this.loadEntity();
    this.loadOutstanding();
    this.loadPerformance();
    this.loadDashboard();
  }

  private loadDashboard(): void {
    this.partyDashboardService.getCustomerDashboard(this.entityId).subscribe({
      next: (data) => this.dashboard.set(data),
      error: () => {},
    });
  }

  private loadEntity() {
    this.customerService.get(this.entityId).subscribe({
      next: (data: any) => { this.entity.set(data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private loadOutstanding() {
    this.reconciliationService.getOutstandingInvoices('Customer', this.entityId).subscribe({
      next: (data: any) => {
        const items = Array.isArray(data) ? data : (data?.items ?? []);
        this.outstandingCount.set(Array.isArray(items) ? items.length : 0);
        this.outstandingTotal.set(
          Array.isArray(items) ? items.reduce((sum: number, i: any) => sum + (i.outstandingAmount ?? 0), 0) : 0
        );
        // Calculate aging buckets from outstanding invoices
        if (Array.isArray(items) && items.length > 0) {
          this.calculateAgingBuckets(items);
        }
        this.outstandingLoading.set(false);
      },
      error: () => this.outstandingLoading.set(false),
    });
  }

  private calculateAgingBuckets(invoices: any[]): void {
    const today = new Date();
    const buckets = [
      { label: '0-30', amount: 0 },
      { label: '31-60', amount: 0 },
      { label: '61-90', amount: 0 },
      { label: '91-120', amount: 0 },
      { label: '120+', amount: 0 },
    ];

    for (const inv of invoices) {
      const dueDate = inv.dueDate ? new Date(inv.dueDate) : null;
      const outstanding = inv.outstandingAmount ?? 0;
      if (outstanding <= 0) continue;

      let daysOverdue = 0;
      if (dueDate) {
        daysOverdue = Math.max(0, Math.floor((today.getTime() - dueDate.getTime()) / (1000 * 60 * 60 * 24)));
      }

      if (daysOverdue <= 30) buckets[0].amount += outstanding;
      else if (daysOverdue <= 60) buckets[1].amount += outstanding;
      else if (daysOverdue <= 90) buckets[2].amount += outstanding;
      else if (daysOverdue <= 120) buckets[3].amount += outstanding;
      else buckets[4].amount += outstanding;
    }

    // Only show buckets that have amounts
    this.agingBuckets.set(buckets.filter(b => b.amount > 0));
  }

  private loadPerformance(): void {
    this.partyPerformanceService.getCustomerPerformance(this.entityId).subscribe({
      next: (data: any) => { this.performance.set(data); this.performanceLoading.set(false); },
      error: () => this.performanceLoading.set(false),
    });
  }

  getBarHeight(amount: number, trend: { amount: number }[]): number {
    const max = Math.max(...trend.map(t => t.amount));
    return max > 0 ? (amount / max) * 100 : 0;
  }
}
