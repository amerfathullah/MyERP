import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { PeriodClosingVoucherService, PcvGlEntryDto } from '../../proxy/accounting/period-closing-voucher.service';
import type { PeriodClosingVoucherDto, CreatePeriodClosingVoucherDto } from '../../proxy/accounting/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { HttpClient } from '@angular/common/http';

@Component({
  standalone: true,
  selector: 'app-period-closing',
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './period-closing.component.html',
})
export class PeriodClosingComponent implements OnInit {
  private service = inject(PeriodClosingVoucherService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private http = inject(HttpClient);
  private l = inject(LocalizationService);

  items = signal<PeriodClosingVoucherDto[]>([]);
  isLoading = signal(false);
  showCreateForm = signal(false);
  expandedPcvId = signal<string | null>(null);
  glEntries = signal<PcvGlEntryDto[]>([]);
  accounts = signal<any[]>([]);
  fiscalYears = signal<any[]>([]);
  companies = signal<any[]>([]);
  /** Resolved account names for display */
  accountNames = signal<Record<string, string>>({});

  form: CreatePeriodClosingVoucherDto = {
    companyId: '',
    postingDate: new Date().toISOString().split('T')[0],
    closingAccountId: '',
    fiscalYearId: '',
    remarks: '',
  };

  ngOnInit() {
    this.loadData();
    // Load companies for dropdown
    this.http.get<any>('/api/app/company?maxResultCount=50&skipCount=0').subscribe({
      next: res => this.companies.set(res.items ?? []),
      error: () => {},
    });
    // Auto-fill companyId from context
    const cid = this.companyContext.currentCompanyId();
    if (cid) {
      this.form.companyId = cid;
      this.loadCompanyAccounts(cid);
      this.loadFiscalYears(cid);
    }
  }

  onCompanyChanged(companyId: string): void {
    this.form.companyId = companyId;
    this.form.closingAccountId = '';
    this.form.fiscalYearId = '';
    if (companyId) {
      this.loadCompanyAccounts(companyId);
      this.loadFiscalYears(companyId);
    }
  }

  private loadCompanyAccounts(companyId: string): void {
    // Load Liability/Equity accounts for closing account selection
    this.http.get<any>(`/api/app/account?companyId=${companyId}&maxResultCount=500&skipCount=0`).subscribe({
      next: res => {
        const accts = (res.items ?? []).filter((a: any) =>
          a.rootType === 'Liability' || a.rootType === 'Equity');
        this.accounts.set(accts);
        // Build name lookup for list display
        const map: Record<string, string> = {};
        (res.items ?? []).forEach((a: any) => { map[a.id] = `${a.accountCode} - ${a.accountName}`; });
        this.accountNames.set(map);
      },
      error: () => {},
    });
  }

  private loadFiscalYears(companyId: string): void {
    this.http.get<any>(`/api/app/fiscal-year?companyId=${companyId}&maxResultCount=50&skipCount=0`).subscribe({
      next: res => this.fiscalYears.set(res.items ?? []),
      error: () => {},
    });
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
    this.service.submit(id).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySubmitted');
        this.loadData();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  cancel(id: string) {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(id).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyCancelled');
          this.loadData();
        },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
      });
    });
  }

  getAccountName(id?: string): string {
    if (!id) return '—';
    return this.accountNames()[id] || '—';
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
