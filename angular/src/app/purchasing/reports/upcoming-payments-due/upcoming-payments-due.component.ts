import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

interface UpcomingPaymentDue {
  invoiceId: string;
  invoiceNumber: string;
  supplierId: string;
  supplierName: string;
  dueDate: string;
  outstandingAmount: number;
  grandTotal: number;
  currencyCode: string;
  daysUntilDue: number;
  weekLabel: string;
  isOverdue: boolean;
}

interface UpcomingPaymentsDueReport {
  totalDueThisWeek: number;
  totalDueNextWeek: number;
  totalDueNext30Days: number;
  totalOverdue: number;
  invoiceCount: number;
  supplierCount: number;
  invoices: UpcomingPaymentDue[];
}

@Component({
  standalone: true,
  selector: 'app-upcoming-payments-due',
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h4><i class="fa fa-calendar-days me-2"></i>{{ 'UpcomingPaymentsDue' | abpLocalization }}</h4>
        <div class="d-flex gap-2">
          <select class="form-select form-select-sm" [(ngModel)]="daysAhead" (change)="loadReport()" style="width: 150px;">
            <option [value]="7">{{ '::NextWeek' | abpLocalization }}</option>
            <option [value]="14">{{ '::Next2Weeks' | abpLocalization }}</option>
            <option [value]="30">{{ '::Next30Days' | abpLocalization }}</option>
            <option [value]="60">{{ '::Next60Days' | abpLocalization }}</option>
            <option [value]="90">{{ '::Next90Days' | abpLocalization }}</option>
          </select>
          @if (report()) {
            <button class="btn btn-outline-secondary btn-sm" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ '::ExportCSV' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <!-- KPI Cards -->
      @if (report(); as r) {
        <div class="row g-3 mb-4">
          <div class="col-6 col-md-3">
            <div class="card border-start border-4 border-danger h-100">
              <div class="card-body py-2">
                <small class="text-muted d-block">{{ '::Overdue' | abpLocalization }}</small>
                <span class="fs-5 fw-bold text-danger">{{ r.totalOverdue | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-start border-4 border-warning h-100">
              <div class="card-body py-2">
                <small class="text-muted d-block">{{ '::DueThisWeek' | abpLocalization }}</small>
                <span class="fs-5 fw-bold text-warning">{{ r.totalDueThisWeek | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-start border-4 border-info h-100">
              <div class="card-body py-2">
                <small class="text-muted d-block">{{ '::DueNextWeek' | abpLocalization }}</small>
                <span class="fs-5 fw-bold text-info">{{ r.totalDueNextWeek | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
          <div class="col-6 col-md-3">
            <div class="card border-start border-4 border-primary h-100">
              <div class="card-body py-2">
                <small class="text-muted d-block">{{ '::TotalDue' | abpLocalization }} ({{ daysAhead }}d)</small>
                <span class="fs-5 fw-bold">{{ r.totalDueNext30Days | number:'1.2-2' }}</span>
                <small class="text-muted d-block mt-1">{{ r.invoiceCount }} {{ '::Invoices' | abpLocalization }} · {{ r.supplierCount }} {{ '::Suppliers' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        </div>

        <!-- Invoice Table grouped by week -->
        @if (r.invoices.length > 0) {
          <div class="card">
            <div class="card-body p-0">
              <div class="table-responsive">
                <table class="table table-hover table-sm mb-0">
                  <thead class="table-light">
                    <tr>
                      <th>{{ '::Supplier' | abpLocalization }}</th>
                      <th>{{ '::InvoiceNumber' | abpLocalization }}</th>
                      <th>{{ '::DueDate' | abpLocalization }}</th>
                      <th class="text-center">{{ '::DaysUntilDue' | abpLocalization }}</th>
                      <th class="text-end">{{ '::Outstanding' | abpLocalization }}</th>
                      <th>{{ '::Week' | abpLocalization }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (inv of r.invoices; track inv.invoiceId) {
                      <tr [class.table-danger]="inv.isOverdue" [class.table-warning]="!inv.isOverdue && inv.daysUntilDue <= 3">
                        <td>{{ inv.supplierName }}</td>
                        <td>
                          <a [routerLink]="['/purchasing/invoices', inv.invoiceId]" class="text-decoration-none">
                            {{ inv.invoiceNumber }}
                          </a>
                        </td>
                        <td>{{ inv.dueDate | date:'dd MMM yyyy' }}</td>
                        <td class="text-center">
                          @if (inv.isOverdue) {
                            <span class="badge bg-danger">{{ inv.daysUntilDue * -1 }}d {{ '::Overdue' | abpLocalization }}</span>
                          } @else if (inv.daysUntilDue <= 3) {
                            <span class="badge bg-warning text-dark">{{ inv.daysUntilDue }}d</span>
                          } @else {
                            <span class="text-muted">{{ inv.daysUntilDue }}d</span>
                          }
                        </td>
                        <td class="text-end font-monospace fw-semibold">{{ inv.outstandingAmount | number:'1.2-2' }}</td>
                        <td><span class="badge bg-light text-dark">{{ inv.weekLabel }}</span></td>
                      </tr>
                    }
                  </tbody>
                  <tfoot class="table-light">
                    <tr>
                      <td colspan="4" class="fw-bold">{{ '::Total' | abpLocalization }}</td>
                      <td class="text-end font-monospace fw-bold">{{ r.totalDueNext30Days | number:'1.2-2' }}</td>
                      <td></td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            </div>
          </div>
        } @else {
          <div class="card">
            <div class="card-body text-center py-5 text-muted">
              <i class="fa fa-check-circle fa-3x mb-3 text-success"></i>
              <p class="mb-0">{{ '::NoUpcomingPaymentsDue' | abpLocalization }}</p>
            </div>
          </div>
        }
      }

      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary"></div>
        </div>
      }
    </div>
  `,
})
export class UpcomingPaymentsDueComponent implements OnInit {
  private http = inject(HttpClient);
  private companyContext = inject(CompanyContextService);

  report = signal<UpcomingPaymentsDueReport | null>(null);
  isLoading = signal(false);
  daysAhead = 30;

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.http
      .get<UpcomingPaymentsDueReport>('/api/app/upcoming-payments-due/report', {
        params: { companyId, daysAhead: this.daysAhead.toString() },
      })
      .subscribe({
        next: (r) => {
          this.report.set(r);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    const rows = r.invoices.map(inv => ({
      Supplier: inv.supplierName,
      Invoice: inv.invoiceNumber,
      'Due Date': inv.dueDate,
      'Days Until Due': inv.daysUntilDue,
      Outstanding: inv.outstandingAmount,
      Week: inv.weekLabel,
    }));
    exportToCsv('upcoming-payments-due.csv', rows, ['Supplier', 'Invoice', 'Due Date', 'Days Until Due', 'Outstanding', 'Week']);
  }
}
