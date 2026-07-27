import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PeriodClosingVoucherService, PcvGlEntryDto } from '../../proxy/accounting/period-closing-voucher.service';
import type { PeriodClosingVoucherDto, CreatePeriodClosingVoucherDto } from '../../proxy/accounting/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  standalone: true,
  selector: 'app-period-closing',
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './period-closing.component.html',
})
export class PeriodClosingComponent implements OnInit {
  private service = inject(PeriodClosingVoucherService);

  items = signal<PeriodClosingVoucherDto[]>([]);
  isLoading = signal(false);
  showCreateForm = signal(false);
  expandedPcvId = signal<string | null>(null);
  glEntries = signal<PcvGlEntryDto[]>([]);

  form: CreatePeriodClosingVoucherDto = {
    companyId: '',
    postingDate: new Date().toISOString().split('T')[0],
    closingAccountId: '',
    fiscalYearId: '',
    remarks: '',
  };

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading.set(true);
    this.service.getList({ maxResultCount: 50 }).subscribe({
      next: res => {
        this.items.set(res.items ?? []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  toggleCreateForm() {
    this.showCreateForm.set(!this.showCreateForm());
  }

  create() {
    if (!this.form.companyId || !this.form.closingAccountId || !this.form.fiscalYearId) return;
    this.service.create(this.form).subscribe({
      next: () => {
        this.loadData();
        this.showCreateForm.set(false);
      },
    });
  }

  getStatusLabel(status?: number): string {
    const labels: Record<number, string> = { 0: 'Draft', 1: 'Submitted', 2: 'Cancelled' };
    return labels[status ?? 0] ?? 'Draft';
  }

  submit(id: string) {
    this.service.submit(id).subscribe(() => this.loadData());
  }

  cancel(id: string) {
    if (!confirm('Cancel this Period Closing Voucher?')) return;
    this.service.cancel(id).subscribe(() => this.loadData());
  }

  toggleGlEntries(id: string) {
    if (this.expandedPcvId() === id) {
      this.expandedPcvId.set(null);
      this.glEntries.set([]);
      return;
    }
    this.expandedPcvId.set(id);
    this.service.getGlEntries(id).subscribe({
      next: entries => this.glEntries.set(entries ?? []),
      error: () => this.glEntries.set([]),
    });
  }
}
