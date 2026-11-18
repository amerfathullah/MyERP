import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { formatDayBookVoucherDate } from '../day-book/day-book.component';
import type {
  PaymentPeriodBasedOnInvoiceDateReportDto,
  PaymentPeriodBasedOnInvoiceDateRowDto,
} from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export interface PartyOption {
  id: string;
  name: string;
}

@Component({
  selector: 'app-payment-period-based-on-invoice-date',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
  ],
  templateUrl: './payment-period-based-on-invoice-date.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class PaymentPeriodBasedOnInvoiceDateComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly today = new Date().toISOString().split('T')[0];
  private readonly defaultFromDate = (() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 3);
    return d.toISOString().split('T')[0];
  })();

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [this.defaultFromDate],
    toDate: [this.today],
    paymentType: ['Incoming', Validators.required],
    partyId: [''],
  });

  companies = signal<CompanyDto[]>([]);
  parties = signal<PartyOption[]>([]);
  report = signal<PaymentPeriodBasedOnInvoiceDateReportDto | null>(null);
  isLoading = signal(false);

  rows = computed(() => this.report()?.rows ?? []);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId() || res.items?.[0]?.id || '';
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        this.loadParties();
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      },
      error: () => {},
    });
  }

  onPaymentTypeChange(): void {
    this.filters.patchValue({ partyId: '' });
    this.loadParties();
    if (this.filters.valid) {
      this.generate();
    }
  }

  private loadParties(): void {
    const paymentType = this.filters.get('paymentType')?.value ?? 'Incoming';
    if (paymentType === 'Outgoing') {
      this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe({
        next: res => {
          this.parties.set((res.items ?? []).filter(s => !!s.id).map(s => ({ id: s.id!, name: s.name ?? '' })));
        },
        error: () => this.parties.set([]),
      });
    } else {
      this.customerService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe({
        next: res => {
          this.parties.set((res.items ?? []).filter(c => !!c.id).map(c => ({ id: c.id!, name: c.name ?? '' })));
        },
        error: () => this.parties.set([]),
      });
    }
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const val = this.filters.getRawValue();
    const paymentType = val.paymentType ?? 'Incoming';
    this.reportingService.getPaymentPeriodBasedOnInvoiceDate({
      companyId: val.companyId!,
      fromDate: val.fromDate || null,
      toDate: val.toDate || null,
      paymentType,
      partyType: val.partyId ? (paymentType === 'Outgoing' ? 'Supplier' : 'Customer') : null,
      partyId: val.partyId || null,
    }).subscribe({
      next: res => {
        this.report.set(res);
        this.isLoading.set(false);
      },
      error: err => {
        this.isLoading.set(false);
        this.toaster.error(err?.error?.error?.message ?? '::FailedToGenerateReport');
      },
    });
  }

  formatDate(value: string | Date | undefined | null): string {
    return formatDayBookVoucherDate(value);
  }

  drillDownPayment(row: PaymentPeriodBasedOnInvoiceDateRowDto): void {
    if (!row.paymentDocumentId) return;
    switch (row.paymentDocumentType) {
      case 'PaymentEntry':
        this.router.navigate(['/accounting/payments', row.paymentDocumentId]);
        break;
      case 'JournalEntry':
        this.router.navigate(['/accounting/journal-entries', row.paymentDocumentId]);
        break;
      case 'SalesInvoice':
        this.router.navigate(['/sales/invoices', row.paymentDocumentId]);
        break;
      case 'PurchaseInvoice':
        this.router.navigate(['/purchasing/invoices', row.paymentDocumentId]);
        break;
      default:
        break;
    }
  }

  drillDownInvoice(row: PaymentPeriodBasedOnInvoiceDateRowDto, event: Event): void {
    event.stopPropagation();
    if (!row.invoiceId) return;
    if (row.invoiceType === 'SalesInvoice') {
      this.router.navigate(['/sales/invoices', row.invoiceId]);
    } else if (row.invoiceType === 'PurchaseInvoice') {
      this.router.navigate(['/purchasing/invoices', row.invoiceId]);
    }
  }

  exportCsv(): void {
    const rows = this.rows();
    if (rows.length === 0) return;

    const val = this.filters.getRawValue();
    const data = rows.map(row => ({
      'Payment Document Type': row.paymentDocumentType ?? '',
      'Payment Document': row.paymentDocumentNo ?? '',
      'Party Type': row.partyType ?? '',
      Party: row.partyName ?? '',
      'Posting Date': this.formatDate(row.postingDate),
      Invoice: row.invoiceNo ?? '',
      'Invoice Posting Date': this.formatDate(row.invoicePostingDate),
      'Payment Due Date': this.formatDate(row.dueDate),
      Amount: (row.amount ?? 0).toFixed(2),
      Remarks: row.remarks ?? '',
      Age: row.age ?? 0,
      '0-30': (row.range0To30 ?? 0).toFixed(2),
      '31-60': (row.range31To60 ?? 0).toFixed(2),
      '61-90': (row.range61To90 ?? 0).toFixed(2),
      '90 Above': (row.range90Above ?? 0).toFixed(2),
      'Delay in Payment (Days)': row.delayInPaymentDays ?? 0,
    }));
    exportToCsv(`payment-period-based-on-invoice-date-${val.paymentType}.csv`, data, [
      'Payment Document Type',
      'Payment Document',
      'Party Type',
      'Party',
      'Posting Date',
      'Invoice',
      'Invoice Posting Date',
      'Payment Due Date',
      'Amount',
      'Remarks',
      'Age',
      '0-30',
      '31-60',
      '61-90',
      '90 Above',
      'Delay in Payment (Days)',
    ]);
  }
}
