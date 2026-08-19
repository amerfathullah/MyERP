import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import type { SupplierDto } from '../proxy/purchasing/models';
import { SupplierHoldType } from '../proxy/purchasing/supplier-hold-type.enum';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { PartyPerformanceService } from '../proxy/core/party-performance.service';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../shared/components/activity-log/activity-log.component';
import { AddressManagerComponent } from '../shared/components/address-manager/address-manager.component';
import { ContactManagerComponent } from '../shared/components/contact-manager/contact-manager.component';

@Component({
  selector: 'app-supplier-detail',
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
    } @else if (entity(); as supplier) {
      <!-- Header -->
      <div class="card mb-4">
        <div class="card-body d-flex align-items-center justify-content-between">
          <div>
            <h3 class="mb-1">
              <i class="fas fa-truck me-2 text-primary"></i>{{ supplier.name }}
            </h3>
            <span class="badge" [class]="supplier.isActive ? 'bg-success' : 'bg-secondary'">
              {{ (supplier.isActive ? '::Active' : '::Inactive') | abpLocalization }}
            </span>
            @if (supplier.holdType) {
              <span class="badge ms-2" [class]="getHoldBadgeClass(supplier.holdType)">
                <i class="fas fa-ban me-1"></i>{{ '::HoldStatus' | abpLocalization }}: {{ getHoldLabel(supplier.holdType) }}
              </span>
            }
            @if (supplier.supplierCode) {
              <span class="badge bg-light text-dark ms-2">{{ supplier.supplierCode }}</span>
            }
          </div>
          <a [routerLink]="['/suppliers', entityId, 'edit']" class="btn btn-outline-primary">
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
                    <td>{{ supplier.name }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Code' | abpLocalization }}</th>
                    <td>{{ supplier.supplierCode || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::TIN' | abpLocalization }}</th>
                    <td class="font-monospace">{{ supplier.tin || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::SST' | abpLocalization }}</th>
                    <td class="font-monospace">{{ supplier.sstRegistrationNumber || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::BRN' | abpLocalization }}</th>
                    <td class="font-monospace">{{ supplier.registrationNumber || '—' }}</td>
                  </tr>
                  @if (supplier.holdType) {
                    <tr>
                      <th class="text-muted">{{ '::HoldStatus' | abpLocalization }}</th>
                      <td><span class="badge bg-danger">{{ (supplier.holdType === 1 ? '::All' : (supplier.holdType === 2 ? '::InvoicesOnly' : '::PaymentsOnly')) | abpLocalization }}</span></td>
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
                    <td>{{ supplier.contactPerson || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Phone' | abpLocalization }}</th>
                    <td>{{ supplier.phone || '—' }}</td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Email' | abpLocalization }}</th>
                    <td>
                      @if (supplier.email) {
                        <a [href]="'mailto:' + supplier.email">{{ supplier.email }}</a>
                      } @else { — }
                    </td>
                  </tr>
                  <tr>
                    <th class="text-muted">{{ '::Website' | abpLocalization }}</th>
                    <td>
                      @if (supplier.website) {
                        <a [href]="supplier.website" target="_blank">{{ supplier.website }}</a>
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
      @if (supplier.address || supplier.city || supplier.state) {
        <div class="card mb-4">
          <div class="card-header"><i class="fas fa-map-marker-alt me-2"></i>{{ '::Address' | abpLocalization }}</div>
          <div class="card-body">
            <p class="mb-0">
              {{ supplier.address }}
              @if (supplier.city) { <br>{{ supplier.city }} }
              @if (supplier.state) { , {{ supplier.state }} }
              @if (supplier.postalCode) { {{ supplier.postalCode }} }
              @if (supplier.country) { <br>{{ supplier.country }} }
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
                <div class="fs-4 fw-bold text-warning">{{ outstandingTotal() | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ '::TotalOutstandingAmount' | abpLocalization }}</div>
              </div>
            </div>
          }
        </div>
      </div>

      <!-- Performance Metrics -->
      <div class="card mb-4">
        <div class="card-header"><i class="fas fa-chart-bar me-2"></i>{{ '::PerformanceMetrics' | abpLocalization }}</div>
        <div class="card-body">
          @if (performanceLoading()) {
            <div class="text-center py-3">
              <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
            </div>
          } @else if (performance(); as perf) {
            <div class="row text-center mb-3">
              <div class="col-sm-3">
                <div class="fs-4 fw-bold text-primary">{{ perf.totalSpend | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ '::TotalSpend' | abpLocalization }}</div>
              </div>
              <div class="col-sm-3">
                <div class="fs-4 fw-bold text-info">{{ perf.totalOrders }}</div>
                <div class="text-muted small">{{ '::TotalOrders' | abpLocalization }}</div>
              </div>
              <div class="col-sm-3">
                <div class="fs-4 fw-bold text-success">{{ perf.avgOrderValue | number:'1.2-2' }}</div>
                <div class="text-muted small">{{ '::AvgOrderValue' | abpLocalization }}</div>
              </div>
              <div class="col-sm-3">
                <div class="fs-4 fw-bold" [class]="perf.onTimeDeliveryPercent >= 80 ? 'text-success' : perf.onTimeDeliveryPercent >= 50 ? 'text-warning' : 'text-danger'">
                  {{ perf.onTimeDeliveryPercent | number:'1.0-0' }}%
                </div>
                <div class="text-muted small">{{ '::OnTimeDelivery' | abpLocalization }}</div>
              </div>
            </div>

            <!-- Spend Trend (6 months) -->
            @if (perf.spendTrend?.length) {
              <div class="mt-3">
                <small class="text-muted">{{ '::SpendTrend' | abpLocalization }} ({{ '::Last6Months' | abpLocalization }})</small>
                <div class="d-flex align-items-end gap-1 mt-2" style="height: 60px;">
                  @for (point of perf.spendTrend; track point.month) {
                    <div class="flex-fill text-center">
                      <div class="bg-primary rounded-top mx-auto" style="width: 80%; min-height: 4px;"
                        [style.height.%]="getBarHeight(point.amount, perf.spendTrend)"></div>
                      <small class="text-muted d-block" style="font-size: 0.6rem;">{{ point.month | slice:5 }}</small>
                    </div>
                  }
                </div>
              </div>
            }
          } @else {
            <p class="text-muted text-center mb-0">{{ '::LoadingPerformanceMetrics' | abpLocalization }}</p>
          }
        </div>
      </div>

      <!-- Quick Actions -->
      <div class="card mb-4">
        <div class="card-body">
          <div class="d-flex flex-wrap gap-2">
            <a [routerLink]="['/purchasing/orders/new']" [queryParams]="{ supplierId: entityId }" class="btn btn-outline-primary">
              <i class="fas fa-file-alt me-1"></i> {{ '::CreatePurchaseOrder' | abpLocalization }}
            </a>
            <a [routerLink]="['/purchasing/invoices/new']" [queryParams]="{ supplierId: entityId }" class="btn btn-outline-primary">
              <i class="fas fa-file-invoice me-1"></i> {{ '::CreateInvoice' | abpLocalization }}
            </a>
            <a [routerLink]="['/accounting/reports/statement-of-accounts']" [queryParams]="{ partyType: 'Supplier', partyId: entityId }" class="btn btn-outline-secondary">
              <i class="fas fa-scroll me-1"></i> {{ '::ViewStatement' | abpLocalization }}
            </a>
          </div>
        </div>
      </div>

      <!-- Activity Log -->
      <!-- Addresses & Contacts -->
      <div class="row mb-4">
        <div class="col-md-6">
          <app-address-manager [partyType]="'Supplier'" [partyId]="entityId" />
        </div>
        <div class="col-md-6">
          <app-contact-manager [partyType]="'Supplier'" [partyId]="entityId" />
        </div>
      </div>

      <app-activity-log [documentType]="'Supplier'" [documentId]="entityId" />
    }
  `,
  styles: [`
    .w-40 { width: 40%; }
  `],
})
export class SupplierDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private supplierService = inject(SupplierService);
  private reconciliationService = inject(PaymentReconciliationService);
  private partyPerformanceService = inject(PartyPerformanceService);

  entity = signal<SupplierDto | null>(null);
  entityId = '';
  loading = signal(true);
  outstandingLoading = signal(true);
  outstandingCount = signal(0);
  outstandingTotal = signal(0);
  performanceLoading = signal(true);
  performance = signal<any>(null);

  ngOnInit() {
    this.entityId = this.route.snapshot.params['id'];
    this.loadEntity();
    this.loadOutstanding();
    this.loadPerformance();
  }

  private loadEntity() {
    this.supplierService.get(this.entityId).subscribe({
      next: (data: any) => { this.entity.set(data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private loadOutstanding() {
    this.reconciliationService.getOutstandingInvoices('Supplier', this.entityId).subscribe({
      next: (data: any) => {
        const items = Array.isArray(data) ? data : (data?.items ?? []);
        this.outstandingCount.set(Array.isArray(items) ? items.length : 0);
        this.outstandingTotal.set(
          Array.isArray(items) ? items.reduce((sum: number, i: any) => sum + (i.outstandingAmount ?? 0), 0) : 0
        );
        this.outstandingLoading.set(false);
      },
      error: () => this.outstandingLoading.set(false),
    });
  }

  private loadPerformance() {
    this.partyPerformanceService.getSupplierPerformance(this.entityId).subscribe({
      next: (data: any) => { this.performance.set(data); this.performanceLoading.set(false); },
      error: () => this.performanceLoading.set(false),
    });
  }

  getBarHeight(amount: number, trend: any[]): number {
    const max = Math.max(...trend.map((p: any) => p.amount || 0), 1);
    return Math.max(5, (amount / max) * 100);
  }

  getHoldBadgeClass(holdType: SupplierHoldType): string {
    switch (holdType) {
      case SupplierHoldType.All: return 'bg-danger';
      case SupplierHoldType.Invoices: return 'bg-warning text-dark';
      case SupplierHoldType.Payments: return 'bg-info text-dark';
      default: return 'bg-secondary';
    }
  }

  getHoldLabel(holdType: SupplierHoldType): string {
    return ['None', 'All', 'Invoices', 'Payments'][holdType] ?? 'None';
  }
}
