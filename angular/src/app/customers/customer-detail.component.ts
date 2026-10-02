import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerService } from '../proxy/sales/customer.service';
import type { CustomerDto, CustomerOverviewDto, CustomerTransactionDto } from '../proxy/sales/models';
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
  selector: 'app-customer-detail',
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
    } @else if (entity(); as customer) {
      <!-- Header -->
      <div class="card mb-3">
        <div class="card-body d-flex flex-wrap align-items-center justify-content-between gap-2">
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
          <div class="d-flex flex-wrap gap-2">
            <a [routerLink]="['/sales/orders/new']" [queryParams]="{ customerId: entityId }" class="btn btn-outline-primary btn-sm">
              <i class="fas fa-file-alt me-1"></i> {{ '::CreateSalesOrder' | abpLocalization }}
            </a>
            <a [routerLink]="['/sales/invoices/new']" [queryParams]="{ customerId: entityId }" class="btn btn-outline-primary btn-sm">
              <i class="fas fa-file-invoice me-1"></i> {{ '::CreateInvoice' | abpLocalization }}
            </a>
            <a [routerLink]="['/accounting/reports/statement-of-accounts']" [queryParams]="{ partyType: 'Customer', partyId: entityId }" class="btn btn-outline-secondary btn-sm">
              <i class="fas fa-scroll me-1"></i> {{ '::ViewStatement' | abpLocalization }}
            </a>
            <a [routerLink]="['/customers', entityId, 'edit']" class="btn btn-primary btn-sm">
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
                  Customer has unallocated advances of
                  <strong>{{ ov.currency }} {{ ov.unallocatedAdvances | number:'1.2-2' }}</strong>.
                </span>
              </div>
              @if ((ov.position?.outstanding?.unpaidCount ?? 0) > 0) {
                <a
                  routerLink="/accounting/payment-reconciliation"
                  [queryParams]="{ partyType: 'Customer', partyId: entityId }"
                  class="btn btn-sm btn-dark">
                  <i class="fas fa-hand-holding-usd me-1"></i>
                  Reconcile with {{ ov.position?.outstanding?.unpaidCount }} unpaid {{ ((ov.position?.outstanding?.unpaidCount ?? 0) === 1 ? 'invoice' : 'invoices') }}
                </a>
              } @else {
                <span class="badge bg-light text-dark border">Credit balance, no invoices to apply it to</span>
              }
            </div>
          }

          <!-- Position KPI Cards -->
          <div class="row g-3 mb-4">
            <!-- Net Sales -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 border-primary shadow-sm">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::NetSales' | abpLocalization }}</div>
                  <div class="fs-4 fw-bold text-dark">
                    {{ ov.currency }} {{ (ov.position?.netSales?.value ?? 0) | number:'1.2-2' }}
                  </div>
                  <div class="d-flex align-items-center justify-content-between mt-2 pt-1 border-top small text-muted">
                    <span>{{ ov.position?.netSales?.count ?? 0 }} invoices</span>
                    @if (ov.position?.netSales?.delta !== null && ov.position?.netSales?.delta !== undefined) {
                      <span class="badge" [class.bg-success]="(ov.position?.netSales?.delta ?? 0) >= 0" [class.bg-danger]="(ov.position?.netSales?.delta ?? 0) < 0">
                        <i class="fas" [class.fa-arrow-up]="(ov.position?.netSales?.delta ?? 0) >= 0" [class.fa-arrow-down]="(ov.position?.netSales?.delta ?? 0) < 0"></i>
                        {{ ov.position?.netSales?.delta | number:'1.1-1' }}%
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
                        <i class="fas fa-clock me-1 text-info"></i>{{ ov.position?.outstanding?.daysToPay }}d DSO
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
                        [queryParams]="{ partyType: 'Customer', partyId: entityId }"
                        class="text-decoration-none text-primary fw-semibold text-truncate"
                        title="Reconcile with {{ ov.position?.outstanding?.unpaidCount }} unpaid {{ ((ov.position?.outstanding?.unpaidCount ?? 0) === 1 ? 'invoice' : 'invoices') }}">
                        <i class="fas fa-hand-holding-usd me-1"></i>
                        Reconcile with {{ ov.position?.outstanding?.unpaidCount }} unpaid {{ ((ov.position?.outstanding?.unpaidCount ?? 0) === 1 ? 'invoice' : 'invoices') }}
                      </a>
                    } @else {
                      <span class="badge bg-light text-dark border">Credit balance</span>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- Credit Usage -->
            <div class="col-sm-6 col-lg-4 col-xl">
              <div class="card h-100 border-start border-4 shadow-sm" [class.border-warning]="(ov.position?.credit?.usedPct ?? 0) >= 70" [class.border-success]="(ov.position?.credit?.usedPct ?? 0) < 70">
                <div class="card-body">
                  <div class="text-muted small mb-1">{{ '::CreditLimit' | abpLocalization }}</div>
                  <div class="fs-5 fw-bold text-dark">
                    @if ((ov.position?.credit?.limit ?? 0) > 0) {
                      {{ ov.currency }} {{ ov.position?.credit?.limit | number:'1.2-2' }}
                    } @else {
                      <span class="text-muted">{{ '::NoLimit' | abpLocalization }}</span>
                    }
                  </div>
                  @if ((ov.position?.credit?.limit ?? 0) > 0) {
                    <div class="mt-2 pt-1 border-top">
                      <div class="d-flex justify-content-between small text-muted mb-1">
                        <span>{{ '::CreditUsage' | abpLocalization }}</span>
                        <span class="fw-semibold" [class.text-danger]="(ov.position?.credit?.usedPct ?? 0) > 100">
                          {{ ov.position?.credit?.usedPct | number:'1.0-0' }}%
                        </span>
                      </div>
                      <div class="progress" style="height: 6px;">
                        <div
                          class="progress-bar"
                          [class.bg-success]="(ov.position?.credit?.usedPct ?? 0) < 70"
                          [class.bg-warning]="(ov.position?.credit?.usedPct ?? 0) >= 70 && (ov.position?.credit?.usedPct ?? 0) < 90"
                          [class.bg-danger]="(ov.position?.credit?.usedPct ?? 0) >= 90"
                          [style.width.%]="(ov.position?.credit?.usedPct ?? 0) > 100 ? 100 : (ov.position?.credit?.usedPct ?? 0)">
                        </div>
                      </div>
                      @if ((ov.position?.credit?.usedPct ?? 0) > 100) {
                        <div class="text-danger small mt-1 fw-semibold">
                          <i class="fas fa-exclamation-circle me-1"></i>Over credit limit
                        </div>
                      }
                    </div>
                  } @else {
                    <div class="mt-2 pt-1 border-top small text-muted">
                      <span>No limit enforced</span>
                    </div>
                  }
                </div>
              </div>
            </div>
          </div>

          <!-- Animated Monthly Sales Trend -->
          <div class="card mb-4 shadow-sm">
            <div class="card-header d-flex align-items-center justify-content-between bg-white py-3">
              <div class="fw-semibold">
                <i class="fas fa-chart-line me-2 text-primary"></i>
                {{ '::SalesTrend' | abpLocalization }} ({{ ov.period }})
              </div>
              @if (ov.trend?.average && ov.trend!.average > 0) {
                <span class="badge bg-light text-dark border">
                  Monthly Avg: {{ ov.currency }} {{ ov.trend?.average | number:'1.0-0' }}
                </span>
              }
            </div>
            <div class="card-body" [class.co-loading]="overviewLoading()" [attr.aria-busy]="overviewLoading()">
              @if ((ov.trend?.points?.length ?? 0) > 0) {
                <div class="trend-chart-container">
                  @for (point of ov.trend?.points; track point.label) {
                    <div class="trend-bar-wrapper">
                      <div class="trend-tooltip">
                        <strong>{{ point.label }}</strong>: {{ ov.currency }} {{ point.value | number:'1.2-2' }}
                        @if (point.isMtd) {
                          <span class="badge bg-info text-dark ms-1">MTD</span>
                        }
                      </div>
                      <div
                        class="trend-bar"
                        [class.regular]="!point.isMtd"
                        [class.mtd]="point.isMtd"
                        [style.height.%]="getTrendBarHeight(point.value ?? 0)">
                      </div>
                      <div class="mt-2 text-center text-muted text-truncate w-100" style="font-size: 0.75rem;">
                        {{ point.label }}
                        @if (point.isMtd) {
                          <span class="text-info font-monospace">*</span>
                        }
                      </div>
                    </div>
                  }
                </div>
                <div class="d-flex justify-content-between align-items-center mt-2 small text-muted">
                  <div>
                    <span class="badge bg-primary me-1">&nbsp;</span> Closed Months
                    <span class="badge bg-info text-dark ms-3 me-1">&nbsp;</span> MTD (Month-To-Date)
                  </div>
                  <div>All values in {{ ov.currency }}</div>
                </div>
              } @else {
                <div class="text-center text-muted py-4">No sales data for this period</div>
              }
            </div>
          </div>

          <!-- Receivables Ageing & Sales Pipeline -->
          <div class="row mb-4">
            <!-- Ageing Breakdown -->
            <div class="col-lg-6 mb-3 mb-lg-0">
              <div class="card h-100 shadow-sm">
                <div class="card-header d-flex justify-content-between align-items-center bg-white py-3">
                  <span class="fw-semibold">
                    <i class="fas fa-history me-2 text-warning"></i>{{ '::ReceivablesAgeing' | abpLocalization }}
                  </span>
                  @if ((ov.ageing?.overdue ?? 0) > 0) {
                    <span class="badge bg-danger">
                      Overdue: {{ ov.currency }} {{ ov.ageing?.overdue | number:'1.2-2' }} ({{ ov.ageing?.overduePct | number:'1.0-0' }}% of unpaid invoices)
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
                    Total Receivables: <strong>{{ ov.currency }} {{ (ov.ageing?.total ?? 0) | number:'1.2-2' }}</strong>
                  </div>
                </div>
              </div>
            </div>

            <!-- Pipeline Breakdown -->
            <div class="col-lg-6">
              <div class="card h-100 shadow-sm">
                <div class="card-header fw-semibold bg-white py-3">
                  <i class="fas fa-stream me-2 text-info"></i>{{ '::SalesPipeline' | abpLocalization }}
                </div>
                <div class="card-body">
                  <div class="row g-2">
                    <!-- Quotations -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Quotations</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.quotations?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">{{ ov.pipeline?.quotations?.count ?? 0 }} open</div>
                      </div>
                    </div>
                    <!-- Delivery -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Delivery to be Made</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.delivery?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">
                          {{ ov.pipeline?.delivery?.count ?? 0 }} orders
                          @if ((ov.pipeline?.delivery?.pastDue ?? 0) > 0) {
                            <span class="badge bg-danger ms-1">{{ ov.pipeline?.delivery?.pastDue }} past due</span>
                          }
                        </div>
                      </div>
                    </div>
                    <!-- Billing -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">To be Billed</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.billing?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">{{ ov.pipeline?.billing?.count ?? 0 }} orders</div>
                      </div>
                    </div>
                    <!-- Invoices -->
                    <div class="col-6">
                      <div class="p-2 border rounded bg-light">
                        <div class="text-muted small">Unpaid Invoices</div>
                        <div class="fs-6 fw-bold text-dark">{{ ov.currency }} {{ (ov.pipeline?.invoices?.value ?? 0) | number:'1.2-2' }}</div>
                        <div class="text-muted" style="font-size: 0.75rem;">
                          {{ ov.pipeline?.invoices?.count ?? 0 }} invoices
                          @if ((ov.pipeline?.invoices?.overdue ?? 0) > 0) {
                            <span class="badge bg-danger ms-1">{{ ov.pipeline?.invoices?.overdue }} overdue</span>
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
                              @if (tx.docType === 'Sales Invoice') {
                                <i class="fas fa-file-invoice text-primary me-1"></i>
                              } @else if (tx.docType === 'Sales Order') {
                                <i class="fas fa-file-alt text-success me-1"></i>
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
                              [class.bg-success]="tx.status === 'Paid' || tx.status === 'Submitted' || tx.status === 'Completed'"
                              [class.bg-warning]="tx.status === 'Unpaid' || tx.status === 'Overdue' || tx.status === 'Pending'"
                              [class.bg-secondary]="tx.status === 'Draft' || tx.status === 'Cancelled'"
                              [class.bg-info]="tx.status === 'ToPay' || tx.status === 'Return'">
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
                        <th class="text-muted">Invoice without Sales Order allowed</th>
                        <td><span class="badge bg-warning text-dark">{{ '::Yes' | abpLocalization }}</span></td>
                      </tr>
                    }
                    @if (customer.dnRequired) {
                      <tr>
                        <th class="text-muted">Invoice without Delivery Note allowed</th>
                        <td><span class="badge bg-info text-dark">{{ '::Yes' | abpLocalization }}</span></td>
                      </tr>
                    }
                    @if (customer.onHold) {
                      <tr>
                        <th class="text-muted">{{ '::OnHold' | abpLocalization }}</th>
                        <td>
                          <span class="badge bg-danger">{{ '::Yes' | abpLocalization }}</span>
                          @if (customer.releaseDate) {
                            <span class="ms-2 text-muted small">({{ '::ReleaseDate' | abpLocalization }}: {{ customer.releaseDate | date }})</span>
                          }
                        </td>
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
      width: 100%;
      max-width: 42px;
      border-radius: 4px 4px 0 0;
      transition: height 0.6s cubic-bezier(0.16, 1, 0.3, 1), background-color 0.2s;
      position: relative;
      cursor: pointer;
    }
    .trend-bar:hover {
      filter: brightness(0.9);
    }
    .trend-bar.regular {
      background: linear-gradient(180deg, #3b82f6 0%, #1d4ed8 100%);
    }
    .trend-bar.mtd {
      background: linear-gradient(180deg, #06b6d4 0%, #0891b2 100%);
      border: 1px dashed #0284c7;
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
export class CustomerDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private customerService = inject(CustomerService);
  private companyService = inject(CompanyService);
  private reconciliationService = inject(PaymentReconciliationService);
  private partyPerformanceService = inject(PartyPerformanceService);
  private partyDashboardService = inject(PartyDashboardService);

  activeTab = signal<'overview' | 'details'>('overview');
  entity = signal<CustomerDto | null>(null);
  entityId = '';
  loading = signal(true);

  // Overview Tab State
  companies = signal<CompanyDto[]>([]);
  selectedCompanyId = signal<string>('');
  selectedPeriod = signal<string>('This fiscal year');
  periodOptions = ['This fiscal year', 'Last 12 months', 'This quarter', 'Last fiscal year'];
  overview = signal<CustomerOverviewDto | null>(null);
  overviewLoading = signal(false);

  // Transactions State
  transactions = signal<CustomerTransactionDto[]>([]);
  transactionsLoading = signal(false);
  selectedDocType = signal<string>('All');
  docTypeOptions = ['All', 'Sales Invoice', 'Sales Order', 'Payment Entry'];

  // Details Tab State
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

  private loadEntity() {
    this.customerService.get(this.entityId).subscribe({
      next: (data: CustomerDto) => {
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
        const cust = this.entity();
        if (list.length === 0 && cust?.companyId) {
          list = [{ id: cust.companyId, name: 'Default Company' } as CompanyDto];
        }
        this.companies.set(list);

        if (cust?.companyId && list.some(c => c.id === cust.companyId)) {
          this.selectedCompanyId.set(cust.companyId);
        } else if (list.length > 0 && list[0].id) {
          this.selectedCompanyId.set(list[0].id);
        }

        if (this.selectedCompanyId()) {
          this.loadOverview();
          this.loadTransactions();
        }
      },
      error: () => {
        const cust = this.entity();
        if (cust?.companyId) {
          this.companies.set([{ id: cust.companyId, name: 'Default Company' } as CompanyDto]);
          this.selectedCompanyId.set(cust.companyId);
          this.loadOverview();
          this.loadTransactions();
        }
      },
    });
  }

  loadOverview(): void {
    const compId = this.selectedCompanyId();
    if (!this.entityId || !compId) return;

    this.overviewLoading.set(true);
    this.customerService.getCustomerOverview({
      customerId: this.entityId,
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

  loadTransactions(): void {
    const compId = this.selectedCompanyId();
    if (!this.entityId || !compId) return;

    this.transactionsLoading.set(true);
    this.customerService.getCustomerTransactions({
      customerId: this.entityId,
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

  getTrendBarHeight(value: number): number {
    const points = this.overview()?.trend?.points ?? [];
    const max = Math.max(...points.map(p => p.value ?? 0), 1);
    return Math.max(4, Math.round(((value ?? 0) / max) * 100));
  }

  getDocLink(tx: CustomerTransactionDto): string[] | null {
    if (!tx.id) return null;
    if (tx.docType === 'Sales Invoice') return ['/sales/invoices', tx.id];
    if (tx.docType === 'Sales Order') return ['/sales/orders', tx.id];
    if (tx.docType === 'Payment Entry') return ['/accounting/payments', tx.id];
    return null;
  }

  private loadDashboard(): void {
    this.partyDashboardService.getCustomerDashboard(this.entityId).subscribe({
      next: (data) => this.dashboard.set(data),
      error: () => {},
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
