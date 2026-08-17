import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';
import type { LhdnStatusReportItemDto, LhdnVatReportDto } from '../../proxy/einvoice/models';

@Component({
  selector: 'app-einvoice-status-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LhdnStatusBadgeComponent, LocalizationPipe],
  templateUrl: './einvoice-status-report.component.html',
  styleUrls: ['./einvoice-status-report.component.scss'],
})
export class EinvoiceStatusReportComponent implements OnInit {
  private fb = new FormBuilder();
  private einvoiceService = inject(EInvoiceService);

  filters = this.fb.group({
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
    status: [''],
    reportType: ['sales']
  });

  statusReportData = signal<LhdnStatusReportItemDto[]>([]);
  vatReportData = signal<LhdnVatReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.generate();
  }

  generate(): void {
    this.isLoading.set(true);
    const { fromDate, toDate, status, reportType } = this.filters.getRawValue();

    if (reportType === 'vat') {
      this.statusReportData.set([]);
      this.einvoiceService.getVatReport({
        fromDate: fromDate || null,
        toDate: toDate || null,
      }).subscribe({
        next: (res) => {
          this.vatReportData.set(res);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
    } else if (reportType === 'purchase') {
      this.vatReportData.set(null);
      this.einvoiceService.getPurchaseStatusReport({
        fromDate: fromDate || null,
        toDate: toDate || null,
        status: status || null,
      }).subscribe({
        next: (res) => {
          this.statusReportData.set(res ?? []);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
    } else {
      this.vatReportData.set(null);
      this.einvoiceService.getSalesStatusReport({
        fromDate: fromDate || null,
        toDate: toDate || null,
        status: status || null,
      }).subscribe({
        next: (res) => {
          this.statusReportData.set(res ?? []);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
    }
  }
}

