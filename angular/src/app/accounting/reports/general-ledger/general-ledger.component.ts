import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { GeneralLedgerService } from '../../../proxy/accounting/general-ledger.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';
import type { AccountDto, GeneralLedgerFilterDto, GeneralLedgerReportDto, GeneralLedgerLineDto } from '../../../proxy/accounting/models';

@Component({
  selector: 'app-general-ledger',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe],
  templateUrl: './general-ledger.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./general-ledger.component.scss'],
})
export class GeneralLedgerComponent implements OnInit {
  private fb = inject(FormBuilder);
  private generalLedgerService = inject(GeneralLedgerService);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    accountId: [''],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
    voucherNumber: [''],
    convertAtReportingDateRate: [false],
    showAmountInCompanyCurrency: [false],
  });

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  report = signal<GeneralLedgerReportDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
          this.onCompanyChange();
        }
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      });
  }

  onCompanyChange(): void {
    const companyId = this.filters.get('companyId')?.value;
    if (companyId) {
      this.accountService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'accountCode asc' })
        .subscribe(res => this.accounts.set(res.items ?? []));
    }
    this.onAccountChange();
  }

  onAccountChange(): void {
    // Both options only mean something for a foreign currency account.
    if (!this.isForeignAccountSelected()) {
      this.filters.patchValue({ convertAtReportingDateRate: false, showAmountInCompanyCurrency: false });
    }
  }

  /** Company currency amounts are shown as posted, so there is nothing to convert at the reporting date rate. */
  onShowAmountInCompanyCurrencyChange(): void {
    if (this.filters.get('showAmountInCompanyCurrency')?.value) {
      this.filters.patchValue({ convertAtReportingDateRate: false });
    }
  }

  isForeignAccountSelected(): boolean {
    const accountId = this.filters.get('accountId')?.value;
    const companyId = this.filters.get('companyId')?.value;
    if (!accountId || !companyId) return false;
    const company = this.companies().find(c => c.id === companyId);
    const account = this.accounts().find(a => a.id === accountId);
    if (!company || !account || !account.currency) return false;
    return !!(company.currencyCode && account.currency !== company.currencyCode);
  }

  generate(): void {
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, accountId, fromDate, toDate, voucherNumber, convertAtReportingDateRate, showAmountInCompanyCurrency } =
      this.filters.getRawValue();
    const params: GeneralLedgerFilterDto = { companyId: companyId ?? undefined, fromDate, toDate };
    if (accountId) params.accountId = accountId;
    if (voucherNumber) params.voucherNumber = voucherNumber;
    if (this.isForeignAccountSelected()) {
      if (showAmountInCompanyCurrency) {
        params.showAmountInCompanyCurrency = true;
      } else if (convertAtReportingDateRate) {
        params.convertAtReportingDateRate = true;
      }
    }

    this.generalLedgerService.getReport(params)
      .subscribe({
        next: data => { this.report.set(data); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.entries?.length) return;
    exportToCsv('general-ledger.csv', r.entries as any[], [
      'postingDate', 'accountCode', 'accountName', 'voucherType', 'voucherNumber',
      'partyType', 'partyName', 'debitAmount', 'creditAmount', 'balance', 'costCenterName', 'description'
    ]);
  }

  getVoucherRoute(entry: GeneralLedgerLineDto): string[] {
    if (!entry.voucherId) return [];
    switch (entry.voucherType) {
      case 'SalesInvoice': return ['/sales/invoices', entry.voucherId];
      case 'PurchaseInvoice': return ['/purchasing/invoices', entry.voucherId];
      case 'PaymentEntry': return ['/accounting/payments', entry.voucherId];
      case 'DeliveryNote': return ['/sales/delivery-notes', entry.voucherId];
      case 'PurchaseReceipt': return ['/purchasing/receipts', entry.voucherId];
      default: return ['/accounting/journal-entries', entry.voucherId];
    }
  }
}
