import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { CustomerGroupService } from '../../../proxy/core/customer-group.service';
import { SupplierGroupService } from '../../../proxy/core/supplier-group.service';
import { AgingReportService } from '../../../proxy/accounting/aging-report.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto, CustomerGroupDto, SupplierGroupDto } from '../../../proxy/core/models';
import type {
  AgingReportDto,
  AgingDetailEntryDto,
  AgingPartySummaryDto,
} from '../../../proxy/accounting/models';

export function buildPartySummaryCsvRows(
  summaries: AgingPartySummaryDto[] | undefined,
  bucketLabels: string[] | undefined,
): Record<string, string | number>[] {
  const labels = bucketLabels ?? [];
  return (summaries ?? []).map(s => {
    const row: Record<string, string | number> = {
      Party: s.partyName ?? '',
      'Party Group': s.partyGroupName ?? '',
      Invoiced: (s.invoicedAmount ?? 0).toFixed(2),
      Paid: (s.paidAmount ?? 0).toFixed(2),
      'Credit Note': (s.creditNoteAmount ?? 0).toFixed(2),
      Advance: (s.advanceAmount ?? 0).toFixed(2),
      Outstanding: (s.outstandingAmount ?? 0).toFixed(2),
      'Not Yet Due': (s.notYetDueAmount ?? 0).toFixed(2),
    };
    labels.forEach((label, idx) => {
      row[`${label} Days`] = (s.bucketTotals?.[idx] ?? 0).toFixed(2);
    });
    row['Total Due'] = (s.totalDue ?? 0).toFixed(2);
    row.Invoices = s.invoiceCount ?? 0;
    return row;
  });
}

@Component({
  selector: 'app-aging-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './aging-report.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./aging-report.component.scss'],
})
export class AgingReportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private agingReportService = inject(AgingReportService);
  private companyService = inject(CompanyService);
  private customerGroupService = inject(CustomerGroupService);
  private supplierGroupService = inject(SupplierGroupService);
  private companyContext = inject(CompanyContextService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    asOfDate: [new Date().toISOString().split('T')[0], Validators.required],
    reportType: ['receivables'],
    partyGroupId: [''],
    ageingBasedOn: ['Due Date'],
    calculateAgeingWith: ['Report Date'],
    fromDate: [''],
    toDate: [''],
    includeRevaluationJournals: [false],
  });

  viewMode: 'details' | 'summary' = 'details';
  companies = signal<CompanyDto[]>([]);
  customerGroups = signal<CustomerGroupDto[]>([]);
  supplierGroups = signal<SupplierGroupDto[]>([]);
  report = signal<AgingReportDto | null>(null);
  isLoading = signal(false);

  get activePartyGroups(): Array<{ id?: string; name?: string | null }> {
    return this.filters.get('reportType')?.value === 'payables'
      ? this.supplierGroups()
      : this.customerGroups();
  }

  ngOnInit(): void {
    const viewParam = this.route.snapshot.queryParamMap.get('view');
    const typeParam = this.route.snapshot.queryParamMap.get('type');
    if (viewParam === 'summary' || viewParam === 'details') {
      this.viewMode = viewParam;
    }
    if (typeParam === 'receivables' || typeParam === 'payables') {
      this.filters.patchValue({ reportType: typeParam });
    }

    this.filters.get('reportType')?.valueChanges.subscribe(() => {
      this.filters.patchValue({ partyGroupId: '' }, { emitEvent: false });
    });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      });

    this.customerGroupService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe(res => this.customerGroups.set(res.items ?? []));

    this.supplierGroupService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe(res => this.supplierGroups.set(res.items ?? []));
  }

  switchView(mode: 'details' | 'summary'): void {
    this.viewMode = mode;
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const {
      companyId,
      asOfDate,
      reportType,
      partyGroupId,
      ageingBasedOn,
      calculateAgeingWith,
      fromDate,
      toDate,
      includeRevaluationJournals,
    } = this.filters.getRawValue();
    const request = {
      companyId: companyId!,
      asOfDate: asOfDate!,
      partyGroupId: partyGroupId || undefined,
      ageingBasedOn: ageingBasedOn || 'Due Date',
      calculateAgeingWith: calculateAgeingWith || 'Report Date',
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
      includeRevaluationJournals: includeRevaluationJournals ?? false,
    };
    const call$ = reportType === 'receivables'
      ? this.agingReportService.getReceivablesAging(request)
      : this.agingReportService.getPayablesAging(request);

    call$.subscribe({
        next: data => { this.report.set(data); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r) return;
    if (this.viewMode === 'summary') {
      const summaries = r.partySummaries ?? [];
      if (!summaries.length) return;
      const rows = buildPartySummaryCsvRows(summaries, r.bucketLabels);
      const bucketCols = (r.bucketLabels ?? []).map(l => `${l} Days`);
      exportToCsv(`${r.reportType}-party-summary.csv`, rows, [
        'Party', 'Party Group', 'Invoiced', 'Paid', 'Credit Note', 'Advance',
        'Outstanding', 'Not Yet Due', ...bucketCols, 'Total Due', 'Invoices',
      ]);
      return;
    }
    const details = r.details ?? [];
    if (details.length) {
      exportToCsv(`${r.reportType}-aging-detail.csv`, details.map(d => ({
        party: d.partyName,
        invoice: d.documentNumber,
        postingDate: d.postingDate,
        dueDate: d.dueDate,
        outstanding: d.outstandingAmount,
        ageDays: d.ageDays,
        bucket: d.bucketLabel,
      })), ['party', 'invoice', 'postingDate', 'dueDate', 'outstanding', 'ageDays', 'bucket']);
    } else {
      const row = r.bucketLabels!.reduce((obj: any, label, i) => {
        obj[label] = r.bucketTotals![i]; return obj;
      }, { total: r.totalOutstanding });
      exportToCsv(`${r.reportType}-aging.csv`, [row], [...r.bucketLabels!, 'total']);
    }
  }

  getSummaryPartyRoute(summary: AgingPartySummaryDto): string[] {
    const type = this.filters.get('reportType')?.value;
    return type === 'receivables'
      ? ['/customers', summary.partyId ?? '']
      : ['/suppliers', summary.partyId ?? ''];
  }

  sumSummaryField(field: 'invoicedAmount' | 'paidAmount' | 'creditNoteAmount' | 'advanceAmount' | 'outstandingAmount' | 'notYetDueAmount' | 'totalDue' | 'invoiceCount'): number {
    return (this.report()?.partySummaries ?? []).reduce((sum, s) => sum + (s[field] ?? 0), 0);
  }

  getInvoiceRoute(entry: AgingDetailEntryDto): string[] {
    const type = this.filters.get('reportType')?.value;
    return type === 'receivables'
      ? ['/sales/invoices', entry.documentId ?? '']
      : ['/purchasing/invoices', entry.documentId ?? ''];
  }

  isOverdue(entry: AgingDetailEntryDto): boolean {
    return (entry.ageDays ?? 0) > 30;
  }

  isSeverelyOverdue(entry: AgingDetailEntryDto): boolean {
    return (entry.ageDays ?? 0) > 90;
  }

  recordPayment(entry: AgingDetailEntryDto): void {
    const type = this.filters.get('reportType')?.value;
    const partyType = type === 'receivables' ? 'Customer' : 'Supplier';
    this.router.navigate(['/accounting/payments/new'], {
      queryParams: {
        partyType,
        againstInvoiceId: entry.documentId,
        amount: entry.outstandingAmount,
        companyId: this.filters.get('companyId')?.value,
      },
    });
  }

  sendReminder(entry: AgingDetailEntryDto): void {
    const type = this.filters.get('reportType')?.value;
    if (type !== 'receivables') return;

    this.agingReportService.sendPaymentReminder({
      partyId: entry.partyId!,
      partyName: entry.partyName ?? '',
      partyType: 'Customer',
      overdueAmount: entry.outstandingAmount ?? 0,
      invoiceCount: 1,
    }).subscribe({
      error: () => {},
    });

    this.router.navigate(['/sales/dunnings/new'], {
      queryParams: {
        customerId: entry.partyId,
        companyId: this.filters.get('companyId')?.value,
      },
    });
    this.toaster.info(this.l.instant('::DunningInitiated'));
  }

  getPartyRoute(entry: AgingDetailEntryDto): string[] {
    const type = this.filters.get('reportType')?.value;
    return type === 'receivables'
      ? ['/customers', entry.partyId ?? '']
      : ['/suppliers', entry.partyId ?? ''];
  }
}
