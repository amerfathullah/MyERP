import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { CompanyService } from '../../../proxy/core/company.service';
import { AccountService } from '../../../proxy/accounting/account.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import type { CompanyDto } from '../../../proxy/core/models';
import type { AccountDto } from '../../../proxy/accounting/models';

interface GLEntry {
  id: string;
  postingDate: string;
  accountCode?: string;
  accountName?: string;
  voucherType?: string;
  voucherId?: string;
  voucherNumber?: string;
  debitAmount: number;
  creditAmount: number;
  description?: string;
}

interface GLReport {
  entries: GLEntry[];
  totalDebit: number;
  totalCredit: number;
  balance: number;
  count: number;
}

@Component({
  selector: 'app-general-ledger',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './general-ledger.component.html',
  styleUrls: ['./general-ledger.component.scss'],
})
export class GeneralLedgerComponent implements OnInit {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    accountId: [''],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
  });

  companies = signal<CompanyDto[]>([]);
  accounts = signal<AccountDto[]>([]);
  report = signal<GLReport | null>(null);
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
  }

  generate(): void {
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, accountId, fromDate, toDate } = this.filters.getRawValue();
    const params: any = { companyId, fromDate, toDate };
    if (accountId) params.accountId = accountId;

    this.http.get<GLReport>('/api/app/general-ledger/report', { params })
      .subscribe({
        next: data => { this.report.set(data); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
  }

  exportCsv(): void {
    const r = this.report();
    if (!r?.entries?.length) return;
    exportToCsv('general-ledger.csv', r.entries, [
      'postingDate', 'accountCode', 'accountName', 'voucherNumber', 'debitAmount', 'creditAmount', 'description'
    ]);
  }
}
