import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SupplierService } from '../proxy/purchasing/supplier.service';
import type { SupplierDto, SupplierOverviewDto, SupplierTransactionDto } from '../proxy/purchasing/models';
import { SupplierHoldType } from '../proxy/purchasing/supplier-hold-type.enum';
import { CompanyService } from '../proxy/core/company.service';
import type { CompanyDto } from '../proxy/core/models';
import { PaymentReconciliationService } from '../proxy/accounting/payment-reconciliation.service';
import { PartyPerformanceService } from '../proxy/core/party-performance.service';
import { PartyDashboardService } from '../proxy/accounting/party-dashboard.service';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../shared/components/activity-log/activity-log.component';
import { AddressManagerComponent } from '../shared/components/address-manager/address-manager.component';
import { ContactManagerComponent } from '../shared/components/contact-manager/contact-manager.component';

@Component({
  selector: 'app-supplier-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    LocalizationPipe,
    BreadcrumbComponent,
    ActivityLogComponent,
    AddressManagerComponent,
    ContactManagerComponent,
  ],
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
      <div class="card mb-3">
        <div class="card-body d-flex flex-wrap align-items-center justify-content-between gap-2">
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
          <div class="d-flex flex-wrap gap-2">
            <a [routerLink]="['/purchasing/orders/new']" [queryParams]="{ supplierId: entityId }" class="btn btn-outline-primary btn-sm">
              <i class="fas fa-file-alt me-1"></i> {{ '::CreatePurchaseOrder' | abpLocalization }}
            </a>
            <a [routerLink]="['/purchasing/invoices/new']" [queryParams]="{ supplierId: entityId }" class="btn btn-outline-primary btn-sm">
              <i class="fas fa-file-invoice me-1"></i> {{ '::CreateInvoice' | abpLocalization }}
            </a>
            <a [routerLink]="['/accounting/reports/statement-of-accounts']" [queryParams]="{ partyType: 'Supplier', partyId: entityId }" class="btn btn-outline-secondary btn-sm">
              <i class="fas fa-scroll me-1"></i> {{ '::ViewStatement' | abpLocalization }}
            </a>
            <a [routerLink]="['/suppliers', entityId, 'edit']" class="btn btn-primary btn-sm">
              <i class="fas fa-pencil-alt me-1"></i> {{ '::Edit' | abpLocalization }}
            </a>
          </div>
        </div>
      </div>

      <!-- Tab Navigation -->
      <ul class="nav nav-tabs mb-4">
        <li class="nav-item">
          <button
            type="button"
            class="nav-link"
            [class.active]="activeTab() === 'overview'"
            (click)="activeTab.set('overview')">
            <i class="fas fa-chart-pie me-1"></i> {{ '::Overview' | abpLocalization }}
          </button>
        </li>
        <li class="nav-item">
          <button
            type="button"
            class="nav-link"
            [class.active]="activeTab() === 'details'"
            (click)="activeTab.set('details')">
            <i class="fas fa-info-circle me-1"></i> {{ '::Details' | abpLocalization }}
          </button>
        </li>
      </ul>

      @if (activeTab() === 'overview') {
        <!-- Filter Controls (Company & Period) -->
        <div class="card mb-4 bg-light">
          <div class="card-body py-2">
            <div class="row g-2 align-items-center">
              @if (companies().length > 1) {
                <div class="col-sm-6 col-md-4">
                  <div class="input-group input-group-sm">
                    <span class="input-group-text"><i class="fas fa-building me-1"></i>{{ '::Company' | abpLocalization }}</span>
                    <select
                      class="form-select"
                      [ngModel]="selectedCompanyId()"
                      (ngModelChange)="onCompanyChange($event)">
                      @for (comp of companies(); track comp.id) {
                        <option [value]="comp.id">{{ comp.name }}</option>
                      }
                    </select>
                  </div>
                </div>
              }
              <div class="col-sm-6 col-md-4">
                <div class="input-group input-group-sm">
                  <span class="input-group-text"><i class="fas fa-calendar-alt me-1"></i>{{ '::Period' | abpLocalization }}</span>
                  <select
                    class="form-select"
                    [ngModel]="selectedPeriod()"
                    (ngModelChange)="onPeriodChange($event)">
                    @for (p of periodOptions; track p) {
                      <option [value]="p">{{ p }}</option>
                    }
                  </select>
                </div>
              </div>
              <div class="col-12 col-md-4 text-md-end text-muted small">
                @if (overviewLoading()) {
                  <span class="spinner-border spinner-border-sm text-primary me-1" role="status"></span>
                  <span>Refreshing...</span>
                } @else if (overview(); as ov) {
                  <span>As of {{ ov.asOfDate | date:'mediumDate' }}</span>
                }
              </div>
            </div>
          </div>
        </div>

        @if (overviewLoading() && !overview()) {
          <div class="d-flex justify-content-center py-5">
            <div class="spinner-border text-primary" role="status"></div>
          </div>
        } @else if (overview(); as ov) {
          <!-- Unallocated Advances Banner -->
          @if (ov.unallocatedAdvances && ov.unallocatedAdvances > 0) {
            <div class="alert alert-warning d-flex flex-wrap align-items-center justify-content-between gap-2 mb-4 shadow-sm" role="alert">
              <div>
                <i class="fas fa-exclamation-triangle me-2 fs-5 text-warning"></i>
                <span>
                  Supplier has unallocated advances of
                  <strong>{{ ov.currency }} {{ ov.unallocatedAdvances | number:'1.2-2' }}</strong>.
                </span>
              </div>
              @if ((ov.position?.outstanding?.unpaidCount ?? 0) > 0) {
                <a
                  routerLink="/accounting/payment-reconciliation"
                  [queryParams]="{ partyType: 'Supplier', partyId: entityId }"
                  class="btn btn-sm btn-dark">
                  <i class="fas fa-hand-holding-usd me-1"></i>
                  Reconcile with {{ ov.position?.outstanding?.unpaidCount }} unpaid {{ ((ov.position?.outstanding?.unpaidCount ?? 0) === 1 ? 'bill' : 'bills') }}
                </a>
              } @else {
                <span class="badge bg-light text-dark border">Debit advance balance, no bills to apply it to</span>
              }
            </div>
          }

          <!-- Position KPI Cards -->
          <div class="row g-3 mb-4">
            <!-- Net Purchases -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 border-primary shadow-sm">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::NetPurchases' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-dark">
                    {{ ov.currency }} {{ (ov.position?.netPurchases?.value ?? 0) | number:'1.2-2' }}
                  </div>
                  <div class="d-flex align-items-center justify-content-between mt-2 pt-1 border-top small text-muted">
                    <span>{{ ov.position?.netPurchases?.count ?? 0 }} bills</span>
                    @if (ov.position?.netPurchases?.delta !== null && ov.position?.netPurchases?.delta !== undefined) {
                      <span class="badge" [class.bg-success]="(ov.position?.netPurchases?.delta ?? 0) >= 0" [class.bg-secondary]="(ov.position?.netPurchases?.delta ?? 0) < 0">
                        <i class="fas" [class.fa-arrow-up]="(ov.position?.netPurchases?.delta ?? 0) >= 0" [class.fa-arrow-down]="(ov.position?.netPurchases?.delta ?? 0) < 0"></i>
                        {{ ov.position?.netPurchases?.delta | number:'1.1-1' }}%
                      </span>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- Total Outstanding -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 border-info shadow-sm">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::TotalOutstandingAmount' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-dark">
                    {{ ov.currency }} {{ (ov.position?.outstanding?.value ?? 0) | number:'1.2-2' }}
                  </div>
                  <div class="d-flex align-items-center justify-content-between mt-2 pt-1 border-top small text-muted">
                    <span>{{ ov.position?.outstanding?.unpaidCount ?? 0 }} unpaid</span>
                    @if (ov.position?.outstanding?.daysToPay !== null && ov.position?.outstanding?.daysToPay !== undefined) {
                      <span class="badge bg-light text-dark border">
                        <i class="fas fa-clock me-1 text-info"></i>{{ ov.position?.outstanding?.daysToPay }}d DPO
                      </span>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- Overdue -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 shadow-sm" [class.border-danger]="(ov.position?.overdue?.value ?? 0) > 0" [class.border-secondary]="(ov.position?.overdue?.value ?? 0) === 0">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::Overdue' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold" [class.text-danger]="(ov.position?.overdue?.value ?? 0) > 0" [class.text-dark]="(ov.position?.overdue?.value ?? 0) === 0">
                    {{ ov.currency }} {{ (ov.position?.overdue?.value ?? 0) | number:'1.2-2' }}
                  </div>
                  <div class="d-flex align-items-center justify-content-between mt-2 pt-1 border-top small text-muted">
                    <span>{{ ov.position?.overdue?.count ?? 0 }} overdue</span>
                    @if (ov.position?.overdue?.delta !== null && ov.position?.overdue?.delta !== undefined) {
                      <span class="badge" [class.bg-success]="(ov.position?.overdue?.delta ?? 0) <= 0" [class.bg-danger]="(ov.position?.overdue?.delta ?? 0) > 0">
                        <i class="fas" [class.fa-arrow-down]="(ov.position?.overdue?.delta ?? 0) <= 0" [class.fa-arrow-up]="(ov.position?.overdue?.delta ?? 0) > 0"></i>
                        {{ ov.position?.overdue?.delta | number:'1.1-1' }}%
                      </span>
                    } @else if ((ov.position?.overdue?.value ?? 0) > 0) {
                      <span class="badge bg-danger">Action required</span>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- Advances -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 shadow-sm" [class.border-warning]="(ov.unallocatedAdvances ?? 0) > 0" [class.border-secondary]="(ov.unallocatedAdvances ?? 0) === 0">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::Advances' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-dark">
                    {{ ov.currency }} {{ (ov.unallocatedAdvances ?? 0) | number:'1.2-2' }}
                  </div>
                  <div class="d-flex align-items-center justify-content-between mt-2 pt-1 border-top small text-muted">
                    @if ((ov.unallocatedAdvances ?? 0) === 0) {
                      <span>No unapplied payments</span>
                    } @else if ((ov.position?.outstanding?.unpaidCount ?? 0) > 0) {
                      <a
                        routerLink="/accounting/payment-reconciliation"
                        [queryParams]="{ partyType: 'Supplier', partyId: entityId }"
                        class="text-decoration-none text-primary fw-semibold text-truncate"
                        title="Reconcile with {{ ov.position?.outstanding?.unpaidCount }} unpaid {{ ((ov.position?.outstanding?.unpaidCount ?? 0) === 1 ? 'bill' : 'bills') }}">
                        <i class="fas fa-hand-holding-usd me-1"></i>
                        Reconcile with {{ ov.position?.outstanding?.unpaidCount }} unpaid {{ ((ov.position?.outstanding?.unpaidCount ?? 0) === 1 ? 'bill' : 'bills') }}
                      </a>
                    } @else {
                      <span class="badge bg-light text-dark border">Advance deposit</span>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- Delivery Performance -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 shadow-sm" [class.border-success]="(ov.delivery?.onTimeRate ?? 0) >= 80" [class.border-warning]="(ov.delivery?.onTimeRate ?? 0) >= 50 && (ov.delivery?.onTimeRate ?? 0) < 80" [class.border-danger]="(ov.delivery?.onTimeRate !== null && ov.delivery?.onTimeRate !== undefined) && ov.delivery!.onTimeRate! < 50">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::OnTimeDelivery' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-dark">
                    @if (ov.delivery?.onTimeRate !== null && ov.delivery?.onTimeRate !== undefined) {
                      {{ ov.delivery?.onTimeRate | number:'1.0-1' }}%
                    } @else {
                      <span class="text-muted">—</span>
                    }
                  </div>
                  <div class="d-flex align-items-center justify-content-between mt-2 pt-1 border-top small text-muted">
                    <span>{{ ov.delivery?.lines ?? 0 }} order lines</span>
                    @if (ov.delivery?.hasScorecard) {
                      <span class="badge bg-info text-dark">Scorecard</span>
                    }
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Purchases vs Payments Monthly Trend Chart -->
          <div class="card mb-4 shadow-sm">
            <div class="card-header d-flex align-items-center justify-content-between bg-white py-3">
              <div class="fw-semibold">
                <i class="fas fa-chart-line me-2 text-primary"></i>
                Purchases vs Payments Trend ({{ ov.period }})
              </div>
              <div class="d-flex gap-2">
                <span class="badge bg-light text-dark border">
                  Total Purchases: {{ ov.currency }} {{ ov.trend?.totalPurchased | number:'1.0-0' }}
                </span>
                <span class="badge bg-light text-dark border">
                  Total Payments: {{ ov.currency }} {{ ov.trend?.totalPaid | number:'1.0-0' }}
                </span>
              </div>
            </div>
            <div class="card-body" [class.co-loading]="overviewLoading()" [attr.aria-busy]="overviewLoading()">
              @if ((ov.trend?.points?.length ?? 0) > 0) {
                <div class="trend-chart-container">
                  @for (point of ov.trend?.points; track point.label) {
                    <div class="trend-bar-wrapper">
                      <div class="trend-tooltip">
                        <strong>{{ point.title || point.label }}</strong><br>
                        Purchased: {{ ov.currency }} {{ point.purchased | number:'1.2-2' }}<br>
                        Paid: {{ ov.currency }} {{ point.paid | number:'1.2-2' }}
                      </div>
                      <div class="d-flex align-items-end gap-1 w-100 justify-content-center" style="height: 100%;">
                        <div
                          class="trend-bar purchased-bar"
                          [style.height.%]="getTrendBarHeight(point.purchased ?? 0)">
                        </div>
                        <div
                          class="trend-bar paid-bar"
                          [style.height.%]="getTrendBarHeight(point.paid ?? 0)">
                        </div>
                      </div>
                      <div class="mt-2 text-center text-muted text-truncate w-100" style="font-size: 0.75rem;">
                        {{ point.label }}
                      </div>
                    </div>
                  }
                </div>
                <div class="d-flex justify-content-between align-items-center mt-2 small text-muted">
                  <div>
                    <span class="badge bg-primary me-1">&nbsp;</span> Purchases
                    <span class="badge bg-success ms-3 me-1">&nbsp;</span> Payments
                  </div>
                  <div>All values in {{ ov.currency }}</div>
                </div>
              } @else {
                <div class="text-center text-muted py-4">No purchases or payment data for this period</div>
              }
            </div>
          </div>

          <!-- Payables Ageing & Purchasing Pipeline -->
          <div class="row mb-4">
            <!-- Ageing Breakdown -->
            <div class="col-lg-6 mb-3 mb-lg-0">
              <div class="card h-100 shadow-sm">
                <div class="card-header d-flex justify-content-between align-items-center bg-white py-3">
                  <span class="fw-semibold">
                    <i class="fas fa-history me-2 text-warning"></i>Payables Ageing
                  </span>
                  @if ((ov.ageing?.overdue ?? 0) > 0) {
                    <span class="badge bg-danger">
                      Overdue: {{ ov.currency }} {{ ov.ageing?.overdue | number:'1.2-2' }} ({{ ov.ageing?.overduePct | number:'1.0-0' }}% of unpaid bills)
                    </span>
                  } @else {
                    <span class="badge bg-success">No overdue</span>
                  }
                </div>
                <div class="card-body">
                  <div class="row g-2 text-center">
                    @for (bucket of ov.ageing?.buckets; track bucket.key) {
                      <div class="col">
                        <div
                          class="p-2 border rounded h-100 d-flex flex-column justify-content-between"
                          [class.border-success]="bucket.key === 'not_due'"
                          [class.border-warning]="bucket.key === 'b1' || bucket.key === 'b2'"
                          [class.border-danger]="bucket.key === 'b3' || bucket.key === 'b4'">
                          <div class="text-muted text-truncate mb-1" style="font-size: 0.7rem;">{{ bucket.label }}</div>
                          <div
                            class="fw-bold"
                            [class.text-success]="bucket.key === 'not_due'"
                            [class.text-warning]="bucket.key === 'b1' || bucket.key === 'b2'"
                            [class.text-danger]="bucket.key === 'b3' || bucket.key === 'b4'"
                            style="font-size: 0.85rem;">
                            {{ bucket.value | number:'1.0-0' }}
                          </div>
                        </div>
                      </div>
                    }
                  </div>
                  <div class="text-muted small mt-3 text-center">
                    Total Payables: <strong>{{ ov.currency }} {{ (ov.ageing?.total ?? 0) | number:'1.2-2' }}</strong>
                  </div>
                </div>
              </div>
            </div>

            <!-- Pipeline Breakdown -->
            <div class="col-lg-6">
              <div class="card h-100 shadow-sm">
                <div class="card-header fw-semibold bg-white py-3">
                  <i class="fas fa-stream me-2 text-info"></i>Purchasing Pipeline
                </div>
                <div class="card-body">
                  <div class="row g-2">
                    <!-- Quotations -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Supplier Quotations</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.quotations?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">
                          {{ ov.pipeline?.quotations?.count ?? 0 }} open
                          @if ((ov.pipeline?.quotations?.expiring ?? 0) > 0) {
                            <span class="badge bg-warning text-dark ms-1">{{ ov.pipeline?.quotations?.expiring }} expiring</span>
                          }
                        </div>
                      </div>
                    </div>
                    <!-- Delivery / Receipt -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Pending Receipt</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.receipt?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">
                          {{ ov.pipeline?.receipt?.count ?? 0 }} orders
                          @if (ov.pipeline?.receipt?.nextDue) {
                            <span class="d-block text-truncate" style="font-size: 0.7rem;">Due: {{ ov.pipeline?.receipt?.nextDue | date:'shortDate' }}</span>
                          }
                        </div>
                      </div>
                    </div>
                    <!-- Billing -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Pending Billing</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.billing?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">{{ ov.pipeline?.billing?.count ?? 0 }} orders received</div>
                      </div>
                    </div>
                    <!-- Late Deliveries -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Overdue Deliveries</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.late?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">
                          {{ ov.pipeline?.late?.count ?? 0 }} orders
                          @if ((ov.pipeline?.late?.daysLate ?? 0) > 0) {
                            <span class="badge bg-danger ms-1">{{ ov.pipeline?.late?.daysLate }}d late</span>
                          }
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Recent Transactions -->
          <div class="card mb-4 shadow-sm">
            <div class="card-header d-flex flex-wrap align-items-center justify-content-between gap-2 bg-white py-3">
              <div class="fw-semibold">
                <i class="fas fa-list-alt me-2 text-primary"></i>
                {{ '::RecentTransactions' | abpLocalization }}
              </div>
              <div class="btn-group btn-group-sm" role="group">
                @for (dt of docTypeOptions; track dt) {
                  <button
                    type="button"
                    class="btn"
                    [class.btn-primary]="selectedDocType() === dt"
                    [class.btn-outline-primary]="selectedDocType() !== dt"
                    (click)="onDocTypeChange(dt)">
                    {{ dt }}
                  </button>
                }
              </div>
            </div>
            <div class="card-body p-0">
              @if (transactionsLoading()) {
                <div class="text-center py-4">
                  <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                </div>
              } @else if (transactions().length === 0) {
                <div class="text-center text-muted py-4">No recent transactions found</div>
              } @else {
                <div class="table-responsive">
                  <table class="table table-hover table-striped mb-0 align-middle">
                    <thead class="table-light">
                      <tr>
                        <th>Date</th>
                        <th>Type</th>
                        <th>Number</th>
                        <th>Status</th>
                        <th class="text-end">Amount</th>
                        <th class="text-end">Outstanding</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (tx of transactions(); track tx.id) {
                        <tr>
                          <td>{{ tx.date | date:'mediumDate' }}</td>
                          <td>
                            <span class="badge bg-light text-dark border">
                              @if (tx.docType === 'Purchase Invoice') {
                                <i class="fas fa-file-invoice text-primary me-1"></i>
                              } @else if (tx.docType === 'Purchase Order') {
                                <i class="fas fa-file-alt text-success me-1"></i>
                              } @else if (tx.docType === 'Purchase Receipt') {
                                <i class="fas fa-boxes text-warning me-1"></i>
                              } @else if (tx.docType === 'Payment Entry') {
                                <i class="fas fa-money-check-alt text-info me-1"></i>
                              }
                              {{ tx.typeLabel }}
                            </span>
                          </td>
                          <td>
                            @if (getDocLink(tx); as link) {
                              <a [routerLink]="link" class="fw-semibold">{{ tx.transactionNumber }}</a>
                            } @else {
                              <span class="fw-semibold">{{ tx.transactionNumber }}</span>
                            }
                          </td>
                          <td>
                            <span
                              class="badge"
                              [class.bg-success]="tx.status === 'Paid' || tx.status === 'Submitted' || tx.status === 'Completed' || tx.status === 'Posted'"
                              [class.bg-warning]="tx.status === 'Unpaid' || tx.status === 'Overdue' || tx.status === 'Pending'"
                              [class.bg-secondary]="tx.status === 'Draft' || tx.status === 'Cancelled'"
                              [class.bg-info]="tx.status === 'Return' || tx.status === 'Debit Note'">
                              {{ tx.status }}
                            </span>
                          </td>
                          <td class="text-end fw-semibold font-monospace">
                            {{ tx.amount | number:'1.2-2' }}
                          </td>
                          <td class="text-end font-monospace">
                            @if (tx.outstandingAmount !== null && tx.outstandingAmount !== undefined) {
                              <span [class.text-danger]="tx.outstandingAmount > 0" [class.text-success]="tx.outstandingAmount === 0">
                                {{ tx.outstandingAmount | number:'1.2-2' }}
                              </span>
                            } @else {
                              <span class="text-muted">—</span>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        }
      } @else {
        <!-- Details Tab (Info Grid, Address, Financial Summary, Performance Metrics, Addresses & Contacts, Activity Log) -->
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
              @if (dashboard(); as dash) {
                <hr class="my-3" />
                <div class="row text-center">
                  <div class="col-sm-6">
                    <div class="fs-5 fw-bold text-success">{{ dash.ytdBilling | number:'1.2-2' }}</div>
                    <div class="text-muted small">{{ '::YtdBilling' | abpLocalization }}</div>
                  </div>
                  <div class="col-sm-6">
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
                </div>
              }
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

        <!-- Addresses & Contacts -->
        <div class="row mb-4">
          <div class="col-md-6">
            <app-address-manager [partyType]="'Supplier'" [partyId]="entityId" />
          </div>
          <div class="col-md-6">
            <app-contact-manager [partyType]="'Supplier'" [partyId]="entityId" />
          </div>
        </div>

        <!-- Activity Log -->
        <app-activity-log [documentType]="'Supplier'" [documentId]="entityId" />
      }
    }
  `,
  styles: [`
    .w-40 { width: 40%; }
    .trend-chart-container {
      height: 180px;
      display: flex;
      align-items: flex-end;
      gap: 8px;
      padding-top: 24px;
      border-bottom: 2px solid #dee2e6;
    }
    .trend-bar-wrapper {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      height: 100%;
      justify-content: flex-end;
      position: relative;
    }
    .trend-bar {
      flex: 1;
      max-width: 20px;
      border-radius: 4px 4px 0 0;
      transition: height 0.6s cubic-bezier(0.16, 1, 0.3, 1), background-color 0.2s;
      position: relative;
      cursor: pointer;
    }
    .trend-bar:hover {
      filter: brightness(0.9);
    }
    .trend-bar.purchased-bar {
      background: linear-gradient(180deg, #3b82f6 0%, #1d4ed8 100%);
    }
    .trend-bar.paid-bar {
      background: linear-gradient(180deg, #10b981 0%, #059669 100%);
    }
    .trend-tooltip {
      visibility: hidden;
      opacity: 0;
      position: absolute;
      bottom: 100%;
      left: 50%;
      transform: translateX(-50%);
      padding: 4px 8px;
      background-color: rgba(15, 23, 42, 0.9);
      color: #fff;
      border-radius: 4px;
      font-size: 0.75rem;
      white-space: nowrap;
      z-index: 10;
      transition: opacity 0.2s;
      pointer-events: none;
      margin-bottom: 6px;
    }
    .trend-bar-wrapper:hover .trend-tooltip {
      visibility: visible;
      opacity: 1;
    }
    .co-loading {
      opacity: 0.4;
      pointer-events: none;
      transition: opacity 150ms ease-in-out;
    }
  `],
})
export class SupplierDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private supplierService = inject(SupplierService);
  private companyService = inject(CompanyService);
  private reconciliationService = inject(PaymentReconciliationService);
  private partyPerformanceService = inject(PartyPerformanceService);
  private partyDashboardService = inject(PartyDashboardService);

  activeTab = signal<'overview' | 'details'>('overview');
  entity = signal<SupplierDto | null>(null);
  entityId = '';
  loading = signal(true);

  // Overview Tab State
  companies = signal<CompanyDto[]>([]);
  selectedCompanyId = signal<string>('');
  selectedPeriod = signal<string>('This fiscal year');
  periodOptions = ['This fiscal year', 'Last 12 months', 'This quarter', 'Last fiscal year'];
  overview = signal<SupplierOverviewDto | null>(null);
  overviewLoading = signal(false);

  // Transactions State
  transactions = signal<SupplierTransactionDto[]>([]);
  transactionsLoading = signal(false);
  selectedDocType = signal<string>('All');
  docTypeOptions = ['All', 'Purchase Invoice', 'Purchase Order', 'Purchase Receipt', 'Payment Entry'];

  // Details Tab State
  outstandingLoading = signal(true);
  outstandingCount = signal(0);
  outstandingTotal = signal(0);
  performanceLoading = signal(true);
  performance = signal<any>(null);
  dashboard = signal<any>(null);

  ngOnInit() {
    this.entityId = this.route.snapshot.params['id'];
    this.loadEntity();
    this.loadOutstanding();
    this.loadPerformance();
    this.loadDashboard();
  }

  private loadDashboard(): void {
    this.partyDashboardService.getSupplierDashboard(this.entityId).subscribe({
      next: (data) => this.dashboard.set(data),
      error: () => {},
    });
  }

  private loadEntity() {
    this.supplierService.get(this.entityId).subscribe({
      next: (data: SupplierDto) => {
        this.entity.set(data);
        this.loading.set(false);
        this.loadCompaniesAndOverview();
      },
      error: () => this.loading.set(false),
    });
  }

  private loadCompaniesAndOverview(): void {
    this.companyService.getList({ maxResultCount: 100 } as any).subscribe({
      next: (res) => {
        let list = res.items ?? [];
        const supp = this.entity();
        if (list.length === 0 && supp?.companyId) {
          list = [{ id: supp.companyId, name: 'Default Company' } as CompanyDto];
        }
        this.companies.set(list);

        if (supp?.companyId && list.some(c => c.id === supp.companyId)) {
          this.selectedCompanyId.set(supp.companyId);
        } else if (list.length > 0 && list[0].id) {
          this.selectedCompanyId.set(list[0].id);
        }

        if (this.selectedCompanyId()) {
          this.loadOverview();
          this.loadTransactions();
        }
      },
      error: () => {
        const supp = this.entity();
        if (supp?.companyId) {
          this.selectedCompanyId.set(supp.companyId);
          this.loadOverview();
          this.loadTransactions();
        }
      },
    });
  }

  onCompanyChange(companyId: string): void {
    this.selectedCompanyId.set(companyId);
    this.loadOverview();
    this.loadTransactions();
  }

  onPeriodChange(period: string): void {
    this.selectedPeriod.set(period);
    this.loadOverview();
  }

  onDocTypeChange(docType: string): void {
    this.selectedDocType.set(docType);
    this.loadTransactions();
  }

  private loadOverview(): void {
    const compId = this.selectedCompanyId();
    if (!compId || !this.entityId) return;

    this.overviewLoading.set(true);
    this.supplierService.getSupplierOverview({
      supplierId: this.entityId,
      companyId: compId,
      period: this.selectedPeriod(),
    }).subscribe({
      next: (data) => {
        this.overview.set(data);
        this.overviewLoading.set(false);
      },
      error: () => this.overviewLoading.set(false),
    });
  }

  private loadTransactions(): void {
    const compId = this.selectedCompanyId();
    if (!compId || !this.entityId) return;

    this.transactionsLoading.set(true);
    this.supplierService.getSupplierTransactions({
      supplierId: this.entityId,
      companyId: compId,
      docType: this.selectedDocType(),
      maxResultCount: 20,
    }).subscribe({
      next: (data) => {
        this.transactions.set(data ?? []);
        this.transactionsLoading.set(false);
      },
      error: () => this.transactionsLoading.set(false),
    });
  }

  getTrendBarHeight(value: number): number {
    const points = this.overview()?.trend?.points ?? [];
    const max = Math.max(...points.map(p => Math.max(p.purchased ?? 0, p.paid ?? 0)), 1);
    if (value <= 0) return 4;
    return Math.max(6, Math.min(100, Math.round((value / max) * 100)));
  }

  getDocLink(tx: SupplierTransactionDto): any[] | null {
    if (tx.docType === 'Purchase Invoice') return ['/purchasing/invoices', tx.id];
    if (tx.docType === 'Purchase Order') return ['/purchasing/orders', tx.id];
    if (tx.docType === 'Purchase Receipt') return ['/purchasing/receipts', tx.id];
    if (tx.docType === 'Payment Entry') return ['/accounting/payments', tx.id];
    return null;
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
