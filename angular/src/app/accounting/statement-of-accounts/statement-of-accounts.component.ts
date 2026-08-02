import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatementOfAccountsService } from '../../proxy/accounting/statement-of-accounts.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../shared/pipes/company-currency.pipe';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { DocumentEmailService } from '../../proxy/sales/document-email.service';
import { exportToCsv } from '../../shared/utils/csv-export';
import type { StatementOfAccountsDto, SupplierStatementDto } from '../../proxy/accounting/models';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, CompanyCurrencyPipe],
  selector: 'app-statement-of-accounts',
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="fa fa-file-contract me-2"></i>{{ 'StatementOfAccounts' | abpLocalization }}</h5>
        @if (customerResult() || supplierResult()) {
          <div class="btn-group btn-group-sm">
            <button class="btn btn-outline-secondary" (click)="exportCsv()">
              <i class="fa fa-download me-1"></i>{{ 'ExportCSV' | abpLocalization }}
            </button>
            <button class="btn btn-outline-success" (click)="openEmailDialog()">
              <i class="fa fa-envelope me-1"></i>{{ '::SendEmail' | abpLocalization }}
            </button>
            <button class="btn btn-outline-primary" (click)="printStatement()">
              <i class="fa fa-print me-1"></i>{{ '::Print' | abpLocalization }}
            </button>
          </div>
        }
      </div>
      <div class="card-body">
        <!-- Party Type Tabs -->
        <ul class="nav nav-tabs mb-3">
          <li class="nav-item">
            <a class="nav-link" [class.active]="partyType === 'Customer'" (click)="switchPartyType('Customer')" role="button">
              <i class="fa fa-users me-1"></i>{{ '::Receivables' | abpLocalization }}
            </a>
          </li>
          <li class="nav-item">
            <a class="nav-link" [class.active]="partyType === 'Supplier'" (click)="switchPartyType('Supplier')" role="button">
              <i class="fa fa-building me-1"></i>{{ '::Payables' | abpLocalization }}
            </a>
          </li>
        </ul>

        <!-- Filters -->
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ partyType === 'Customer' ? ('::Customer' | abpLocalization) : ('::Supplier' | abpLocalization) }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="partyId" (ngModelChange)="onPartyChanged()">
              <option value="">-- {{ '::Select' | abpLocalization }} --</option>
              @if (partyType === 'Customer') {
                @for (c of customers(); track c.id) {
                  <option [value]="c.id">{{ c.customerName }}</option>
                }
              } @else {
                @for (s of suppliers(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::From' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="fromDate">
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::To' | abpLocalization }}</label>
            <input type="date" class="form-control form-control-sm" [(ngModel)]="toDate">
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm w-100" (click)="generate()" [disabled]="!partyId || isLoading()">
              @if (isLoading()) { <i class="fa fa-spinner fa-spin me-1"></i> }
              {{ '::GenerateReport' | abpLocalization }}
            </button>
          </div>
        </div>

        <!-- Customer Statement -->
        @if (partyType === 'Customer' && customerResult(); as r) {
          <ng-container *ngTemplateOutlet="statementView; context: { $implicit: r }"></ng-container>
        }

        <!-- Supplier Statement -->
        @if (partyType === 'Supplier' && supplierResult(); as r) {
          <ng-container *ngTemplateOutlet="statementView; context: { $implicit: r }"></ng-container>
        }

        @if (!customerResult() && !supplierResult()) {
          <div class="text-center text-muted py-5">
            <i class="fa fa-file-contract fa-3x mb-3 opacity-25"></i>
            <p>{{ '::SelectCustomerToGenerateStatement' | abpLocalization }}</p>
          </div>
        }
      </div>
    </div>

    <!-- Reusable statement template -->
    <ng-template #statementView let-r>
      <!-- KPI Cards -->
      <div class="row g-2 mb-3">
        <div class="col-md-3">
          <div class="border rounded p-2 text-center">
            <small class="text-muted">{{ '::OpeningBalance' | abpLocalization }}</small>
            <div class="fw-bold">{{ r.openingBalance | number:'1.2-2' }}</div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="border rounded p-2 text-center">
            <small class="text-muted">{{ '::TotalDebit' | abpLocalization }}</small>
            <div class="fw-bold text-primary">{{ r.totalDebit | number:'1.2-2' }}</div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="border rounded p-2 text-center">
            <small class="text-muted">{{ '::TotalCredit' | abpLocalization }}</small>
            <div class="fw-bold text-success">{{ r.totalCredit | number:'1.2-2' }}</div>
          </div>
        </div>
        <div class="col-md-3">
          <div class="border rounded p-2 text-center bg-light">
            <small class="text-muted">{{ '::ClosingBalance' | abpLocalization }}</small>
            <div class="fw-bold" [class.text-danger]="(r.closingBalance ?? 0) > 0">
              {{ r.closingBalance | number:'1.2-2' }}
            </div>
          </div>
        </div>
      </div>

      <!-- Aging Summary (quick view within statement) -->
      @if (getOverdueEntries(r).length > 0) {
        <div class="alert alert-warning d-flex align-items-center mb-3">
          <i class="fa fa-exclamation-triangle me-2"></i>
          <span>
            <strong>{{ getOverdueEntries(r).length }}</strong> {{ '::OverdueInvoices' | abpLocalization }}
            — {{ '::TotalOutstanding' | abpLocalization }}: <strong>{{ '' | companyCurrency }} {{ getOverdueTotal(r) | number:'1.2-2' }}</strong>
          </span>
        </div>
      }

      <!-- Statement Table -->
      <div class="table-responsive">
        <table class="table table-sm table-hover">
          <thead class="table-light">
            <tr>
              <th>{{ '::Date' | abpLocalization }}</th>
              <th>{{ '::VoucherType' | abpLocalization }}</th>
              <th>{{ '::VoucherNumber' | abpLocalization }}</th>
              <th class="text-end">{{ '::Debit' | abpLocalization }}</th>
              <th class="text-end">{{ '::Credit' | abpLocalization }}</th>
              <th class="text-end">{{ '::Balance' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            <tr class="table-secondary">
              <td colspan="3"><strong>{{ '::OpeningBalance' | abpLocalization }}</strong></td>
              <td></td><td></td>
              <td class="text-end"><strong>{{ r.openingBalance | number:'1.2-2' }}</strong></td>
            </tr>
            @for (entry of r.entries; track $index) {
              <tr [class.table-danger]="isEntryOverdue(entry)">
                <td>{{ entry.date | date:'dd/MM/yyyy' }}</td>
                <td>
                  <span class="badge" [class]="getEntryBadgeClass(entry)">
                    {{ entry.documentType }}
                  </span>
                </td>
                <td>{{ entry.documentNumber }}</td>
                <td class="text-end">{{ entry.debitAmount ? (entry.debitAmount | number:'1.2-2') : '' }}</td>
                <td class="text-end text-success">{{ entry.creditAmount ? (entry.creditAmount | number:'1.2-2') : '' }}</td>
                <td class="text-end fw-bold" [class.text-danger]="(entry.runningBalance ?? 0) > 0">
                  {{ entry.runningBalance | number:'1.2-2' }}
                </td>
              </tr>
            }
            <tr class="table-dark">
              <td colspan="3"><strong>{{ '::ClosingBalance' | abpLocalization }}</strong></td>
              <td class="text-end"><strong>{{ r.totalDebit | number:'1.2-2' }}</strong></td>
              <td class="text-end"><strong>{{ r.totalCredit | number:'1.2-2' }}</strong></td>
              <td class="text-end"><strong>{{ r.closingBalance | number:'1.2-2' }}</strong></td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Transaction Count Summary -->
      <div class="text-muted small mt-2">
        {{ r.entries?.length ?? 0 }} {{ '::Entries' | abpLocalization }} |
        {{ '::From' | abpLocalization }}: {{ fromDate | date:'dd/MM/yyyy' }}
        {{ '::To' | abpLocalization }}: {{ toDate | date:'dd/MM/yyyy' }}
      </div>
    </ng-template>

    <!-- Email Dialog -->
    @if (showEmailDialog()) {
      <div class="modal show d-block" tabindex="-1" style="background: rgba(0,0,0,0.5)">
        <div class="modal-dialog">
          <div class="modal-content">
            <div class="modal-header">
              <h6 class="modal-title"><i class="fa fa-envelope me-2"></i>{{ '::SendStatement' | abpLocalization }}</h6>
              <button type="button" class="btn-close" (click)="showEmailDialog.set(false)"></button>
            </div>
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label">{{ '::RecipientEmail' | abpLocalization }}</label>
                <input type="email" class="form-control" [(ngModel)]="emailRecipient"
                  [placeholder]="'::Placeholder:Email' | abpLocalization">
              </div>
              <div class="mb-3">
                <label class="form-label">{{ '::CcEmails' | abpLocalization }}</label>
                <input type="text" class="form-control" [(ngModel)]="emailCc"
                  [placeholder]="'::Placeholder:CommaSeparatedEmails' | abpLocalization">
              </div>
              <div class="form-check mb-3">
                <input type="checkbox" class="form-check-input" id="attachPdf" [(ngModel)]="attachPdf">
                <label class="form-check-label" for="attachPdf">{{ '::AttachPdf' | abpLocalization }}</label>
              </div>
            </div>
            <div class="modal-footer">
              <button class="btn btn-secondary btn-sm" (click)="showEmailDialog.set(false)">{{ '::Cancel' | abpLocalization }}</button>
              <button class="btn btn-success btn-sm" (click)="sendEmail()" [disabled]="!emailRecipient || isSendingEmail()">
                @if (isSendingEmail()) { <i class="fa fa-spinner fa-spin me-1"></i> }
                <i class="fa fa-paper-plane me-1"></i>{{ '::Send' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>
    }

    <!-- Print Layout (hidden on screen, visible only in print) -->
    @if (customerResult() || supplierResult()) {
      <div class="d-none d-print-block soa-print-layout">
        <div class="text-center mb-4">
          <h4>STATEMENT OF ACCOUNT</h4>
          <p class="mb-0">{{ getPartyName() }}</p>
          <small>{{ '::From' | abpLocalization }}: {{ fromDate }} — {{ '::To' | abpLocalization }}: {{ toDate }}</small>
        </div>
        <table class="table table-bordered table-sm" style="font-size: 11px;">
          <thead>
            <tr class="table-dark">
              <th>Date</th><th>Type</th><th>Number</th>
              <th class="text-end">Debit</th><th class="text-end">Credit</th><th class="text-end">Balance</th>
            </tr>
          </thead>
          <tbody>
            @for (entry of getActiveResult()?.entries ?? []; track $index) {
              <tr>
                <td>{{ entry.date | date:'dd/MM/yyyy' }}</td>
                <td>{{ entry.documentType }}</td>
                <td>{{ entry.documentNumber }}</td>
                <td class="text-end">{{ entry.debitAmount ? (entry.debitAmount | number:'1.2-2') : '' }}</td>
                <td class="text-end">{{ entry.creditAmount ? (entry.creditAmount | number:'1.2-2') : '' }}</td>
                <td class="text-end">{{ entry.runningBalance | number:'1.2-2' }}</td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr class="fw-bold">
              <td colspan="3">Closing Balance</td>
              <td class="text-end">{{ getActiveResult()?.totalDebit | number:'1.2-2' }}</td>
              <td class="text-end">{{ getActiveResult()?.totalCredit | number:'1.2-2' }}</td>
              <td class="text-end">{{ getActiveResult()?.closingBalance | number:'1.2-2' }}</td>
            </tr>
          </tfoot>
        </table>
        <p class="small text-muted mt-4">Generated on {{ today | date:'dd/MM/yyyy HH:mm' }}</p>
      </div>
    }
  `,
  styles: [`
    @media print {
      :host ::ng-deep .card { display: none !important; }
      .soa-print-layout { display: block !important; }
    }
    .nav-link { cursor: pointer; }
  `]
})
export class StatementOfAccountsComponent implements OnInit {
  private statementService = inject(StatementOfAccountsService);
  private companyContext = inject(CompanyContextService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private emailService = inject(DocumentEmailService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  customers = signal<any[]>([]);
  suppliers = signal<any[]>([]);
  customerResult = signal<StatementOfAccountsDto | null>(null);
  supplierResult = signal<SupplierStatementDto | null>(null);
  isLoading = signal(false);

  partyType: 'Customer' | 'Supplier' = 'Customer';
  partyId = '';
  fromDate = new Date(new Date().getFullYear(), 0, 1).toISOString().substring(0, 10);
  toDate = new Date().toISOString().substring(0, 10);
  today = new Date();

  showEmailDialog = signal(false);
  emailRecipient = '';
  emailCc = '';
  attachPdf = true;
  isSendingEmail = signal(false);

  ngOnInit() {
    this.loadCustomers();
    this.loadSuppliers();
  }

  private loadCustomers(): void {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: r => this.customers.set(r.items ?? []),
      error: () => {}
    });
  }

  private loadSuppliers(): void {
    this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (r: any) => this.suppliers.set(r.items ?? []),
      error: () => {}
    });
  }

  switchPartyType(type: 'Customer' | 'Supplier'): void {
    this.partyType = type;
    this.partyId = '';
    this.customerResult.set(null);
    this.supplierResult.set(null);
  }

  onPartyChanged(): void {
    this.customerResult.set(null);
    this.supplierResult.set(null);
  }

  generate(): void {
    if (!this.partyId) return;
    const companyId = this.companyContext.currentCompanyId() || '';
    this.isLoading.set(true);

    if (this.partyType === 'Customer') {
      this.statementService.getCustomerStatement(this.partyId, companyId, this.fromDate, this.toDate).subscribe({
        next: data => { this.customerResult.set(data); this.isLoading.set(false); },
        error: () => { this.toaster.error('::FailedToLoad'); this.isLoading.set(false); }
      });
    } else {
      this.statementService.getSupplierStatement(this.partyId, companyId, this.fromDate, this.toDate).subscribe({
        next: data => { this.supplierResult.set(data); this.isLoading.set(false); },
        error: () => { this.toaster.error('::FailedToLoad'); this.isLoading.set(false); }
      });
    }
  }

  getActiveResult(): any {
    return this.partyType === 'Customer' ? this.customerResult() : this.supplierResult();
  }

  getPartyName(): string {
    if (this.partyType === 'Customer') {
      return this.customers().find(c => c.id === this.partyId)?.customerName ?? '';
    }
    return this.suppliers().find(s => s.id === this.partyId)?.name ?? '';
  }

  getEntryBadgeClass(entry: any): string {
    const t = entry.documentType?.toLowerCase() ?? '';
    if (t.includes('invoice')) return 'bg-primary';
    if (t.includes('payment')) return 'bg-success';
    if (t.includes('credit') || t.includes('debit')) return 'bg-warning text-dark';
    return 'bg-secondary';
  }

  isEntryOverdue(entry: any): boolean {
    if (!entry.dueDate || entry.creditAmount) return false;
    return new Date(entry.dueDate) < new Date() && (entry.debitAmount ?? 0) > 0;
  }

  getOverdueEntries(r: any): any[] {
    return (r.entries ?? []).filter((e: any) => this.isEntryOverdue(e));
  }

  getOverdueTotal(r: any): number {
    return this.getOverdueEntries(r).reduce((sum: number, e: any) => sum + (e.debitAmount ?? 0), 0);
  }

  exportCsv(): void {
    const r = this.getActiveResult();
    if (!r) return;
    const partyName = this.getPartyName().replace(/\s+/g, '_');
    const rows = r.entries.map((e: any) => ({
      Date: e.date, Type: e.documentType, Number: e.documentNumber,
      Debit: e.debitAmount, Credit: e.creditAmount, Balance: e.runningBalance
    }));
    exportToCsv(`statement-${partyName}-${this.fromDate}-to-${this.toDate}.csv`,
      rows, ['Date', 'Type', 'Number', 'Debit', 'Credit', 'Balance']);
  }

  openEmailDialog(): void {
    const party = this.partyType === 'Customer'
      ? this.customers().find(c => c.id === this.partyId)
      : this.suppliers().find(s => s.id === this.partyId);
    this.emailRecipient = party?.email ?? '';
    this.emailCc = '';
    this.attachPdf = true;
    this.showEmailDialog.set(true);
  }

  sendEmail(): void {
    if (!this.emailRecipient) return;
    this.isSendingEmail.set(true);
    this.emailService.sendStatementEmail({
      recipientEmail: this.emailRecipient,
      ccEmails: this.emailCc || undefined,
      partyType: this.partyType,
      partyId: this.partyId,
      fromDate: this.fromDate,
      toDate: this.toDate,
      attachPdf: this.attachPdf
    } as any).subscribe({
      next: () => {
        this.isSendingEmail.set(false);
        this.showEmailDialog.set(false);
        this.toaster.success(this.l.instant('::SuccessfullySent'));
      },
      error: () => {
        this.isSendingEmail.set(false);
        this.toaster.error('::FailedToSendEmail');
      }
    });
  }

  printStatement(): void {
    window.print();
  }
}
