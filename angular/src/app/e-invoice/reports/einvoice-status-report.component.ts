import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { HttpClient } from '@angular/common/http';
import { LhdnStatusBadgeComponent } from '../../shared/components/lhdn-status-badge/lhdn-status-badge.component';

@Component({
  selector: 'app-einvoice-status-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LhdnStatusBadgeComponent],
  templateUrl: './einvoice-status-report.component.html',
  styleUrls: ['./einvoice-status-report.component.scss'],
})
export class EinvoiceStatusReportComponent implements OnInit {
  private fb = new FormBuilder();
  private http = inject(HttpClient);

  filters = this.fb.group({ fromDate: [new Date(new Date().getFullYear(), 0, 1)], toDate: [new Date()], status: [''], documentType: ['sales'] });
  data = signal<any[]>([]);
  isLoading = signal(false);

  ngOnInit(): void {
    this.generate();
  }

  generate(): void {
    this.isLoading.set(true);
    const { fromDate, toDate, status, documentType } = this.filters.getRawValue();
    this.http.get<any>('/api/app/e-invoice', {
      params: {
        skipCount: '0', maxResultCount: '100',
        ...(status ? { status } : {}),
      }
    }).subscribe({
      next: (res) => {
        this.data.set(res.items ?? res ?? []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
