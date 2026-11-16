import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportingService } from '../../../proxy/accounting/reporting.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { AccountSubType } from '../../../proxy/accounting/account-sub-type.enum';
import { CompanyService } from '../../../proxy/core/company.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import { SupplierService } from '../../../proxy/purchasing/supplier.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { CompanyCurrencyPipe } from '../../../shared/pipes/company-currency.pipe';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type {
  AccountDto,
  PartyTrialBalanceReportDto,
  PartyTrialBalanceRowDto,
} from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';

export interface PartySelectOption {
  id: string;
  name: string;
}

export function buildPartyTrialBalanceCsvRows(
  rows: PartyTrialBalanceRowDto[] | undefined,
): Record<string, string | number>[] {
  return (rows ?? []).map(r => ({
    Party: r.partyName ?? '',
    'Party Type': r.partyType ?? '',
    'Opening Debit': (r.openingDebit ?? 0).toFixed(2),
    'Opening Credit': (r.openingCredit ?? 0).toFixed(2),
    Debit: (r.debit ?? 0).toFixed(2),
    Credit: (r.credit ?? 0).toFixed(2),
    'Closing Debit': (r.closingDebit ?? 0).toFixed(2),
    'Closing Credit': (r.closingCredit ?? 0).toFixed(2),
    Currency: r.currency ?? '',
  }));
}

@Component({
  selector: 'app-trial-balance-for-party',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    PageModule,
    LocalizationPipe,
    CompanyCurrencyPipe,
  ],
  templateUrl: './trial-balance-for-party.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class TrialBalanceForPartyComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private reportingService = inject(ReportingService);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  private readonly firstOfYear = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
  private readonly today = new Date().toISOString().split('T')[0];

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [this.firstOfYear, Validators.required],
    toDate: [this.today, Validators.required],
    partyType: ['Customer', Validators.required],
    partyId: [''],
    accountId: [''],
    excludeZeroBalanceParties: [true],
    showZeroValues: [false],
  });

  companies = signal<CompanyDto[]>([]);
  parties = signal<PartySelectOption[]>([]);
  accounts = signal<AccountDto[]>([]);
  report = signal<PartyTrialBalanceReportDto | null>(null);
  isLoading = signal(false);

  rows = computed(() => this.report()?.rows ?? []);
  partyCount = computed(() => this.rows().length);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        this.loadAccounts();
        this.loadParties();
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      },
      error: () => {},
    });
  }

  onCompanyChange(): void {
    this.filters.patchValue({ accountId: '', partyId: '' });
    this.loadAccounts();
    this.loadParties();
  }

  onPartyTypeChange(): void {
    this.filters.patchValue({ accountId: '', partyId: '' });
    this.loadAccounts();
    this.loadParties();
    if (this.filters.valid) {
      this.generate();
    }
  }

  private loadAccounts(): void {
    const companyId = this.filters.get('companyId')?.value;
    if (!companyId) {
      this.accounts.set([]);
      return;
    }
    const partyType = this.filters.get('partyType')?.value;
    const subType = partyType === 'Supplier'
      ? AccountSubType.AccountsPayable
      : AccountSubType.AccountsReceivable;
    this.accountService.getList({
      companyId,
      accountSubType: subType,
      isGroup: false,
      skipCount: 0,
      maxResultCount: 200,
      sorting: '',
    }).subscribe({
      next: res => this.accounts.set(res.items ?? []),
      error: () => this.accounts.set([]),
    });
  }

  private loadParties(): void {
    const partyType = this.filters.get('partyType')?.value;
    if (partyType === 'Supplier') {
      this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe({
        next: res => this.parties.set((res.items ?? []).map(s => ({ id: s.id!, name: s.name ?? '' }))),
        error: () => this.parties.set([]),
      });
    } else {
      this.customerService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' }).subscribe({
        next: res => this.parties.set((res.items ?? []).map(c => ({ id: c.id!, name: c.name ?? '' }))),
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
    this.reportingService.getTrialBalanceForParty({
      companyId: val.companyId!,
      fromDate: val.fromDate!,
      toDate: val.toDate!,
      partyType: val.partyType ?? 'Customer',
      partyId: val.partyId || null,
      accountId: val.accountId || null,
      excludeZeroBalanceParties: val.excludeZeroBalanceParties ?? true,
      showZeroValues: val.showZeroValues ?? false,
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

  drillDown(row: PartyTrialBalanceRowDto): void {
    const val = this.filters.getRawValue();
    this.router.navigate(['/accounting/reports/party-ledger'], {
      queryParams: {
        partyType: row.partyType ?? val.partyType,
        partyId: row.partyId,
        fromDate: val.fromDate,
        toDate: val.toDate,
      },
    });
  }

  exportCsv(): void {
    const csvRows = buildPartyTrialBalanceCsvRows(this.rows());
    if (csvRows.length === 0) return;
    const r = this.report();
    csvRows.push({
      Party: 'TOTAL',
      'Party Type': r?.partyType ?? '',
      'Opening Debit': (r?.totalOpeningDebit ?? 0).toFixed(2),
      'Opening Credit': (r?.totalOpeningCredit ?? 0).toFixed(2),
      Debit: (r?.totalDebit ?? 0).toFixed(2),
      Credit: (r?.totalCredit ?? 0).toFixed(2),
      'Closing Debit': (r?.totalClosingDebit ?? 0).toFixed(2),
      'Closing Credit': (r?.totalClosingCredit ?? 0).toFixed(2),
      Currency: r?.currency ?? '',
    });
    exportToCsv('trial-balance-for-party.csv', csvRows, [
      'Party',
      'Party Type',
      'Opening Debit',
      'Opening Credit',
      'Debit',
      'Credit',
      'Closing Debit',
      'Closing Credit',
      'Currency',
    ]);
  }
}
