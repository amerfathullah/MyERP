import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyService } from '../../../proxy/core/company.service';
import { AgingReportService } from '../../../proxy/accounting/aging-report.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';
import type { AgingReportDto, AgingDetailEntryDto } from '../../../proxy/accounting/models';

@Component({
  selector: 'app-aging-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './aging-report.component.html',
  styleUrls: ['./aging-report.component.scss'],
})
export class AgingReportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private agingReportService = inject(AgingReportService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    asOfDate: [new Date().toISOString().split('T')[0], Validators.required],
    reportType: ['receivables'],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<AgingReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
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
  }

  generate(): void {
    if (this.filters.invalid) {
      this.filters.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    const { companyId, asOfDate, reportType } = this.filters.getRawValue();
    const request = { companyId: companyId!, asOfDate: asOfDate! };
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
