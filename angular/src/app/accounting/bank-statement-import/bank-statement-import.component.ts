import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BankStatementImportService } from '../../proxy/accounting/bank-statement-import.service';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { BankAccountDto } from '../../proxy/accounting/models';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-bank-statement-import',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  templateUrl: './bank-statement-import.component.html',
})
export class BankStatementImportComponent implements OnInit {
  private bankStatementImportService = inject(BankStatementImportService);
  private bankAccountService = inject(BankAccountService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  bankAccountId = signal<string>('');
  companyId = signal<string>('');
  format = signal<'csv' | 'mt940'>('csv');
  fileContent = signal<string>('');
  fileName = signal<string>('');
  importing = signal(false);
  result = signal<{ importedCount: number; skippedCount: number; errors: string[] } | null>(null);

  companies = signal<CompanyDto[]>([]);
  bankAccounts = signal<BankAccountDto[]>([]);

  ngOnInit(): void {
    this.loadCompanies();
  }

  loadCompanies(): void {
    this.companyService.getList({ maxResultCount: 100 } as any).subscribe({
      next: (res: any) => {
        const items = res?.items ?? [];
        this.companies.set(items);
        if (items.length > 0 && !this.companyId()) {
          this.onCompanyChange(items[0].id);
        }
      },
    });
  }

  onCompanyChange(companyId: string): void {
    this.companyId.set(companyId);
    this.bankAccountId.set('');
    if (companyId) {
      this.bankAccountService.getList({
        companyId,
        isCompanyAccount: true,
        maxResultCount: 100,
      } as any).subscribe({
        next: (res: any) => {
          const accounts = res?.items ?? [];
          this.bankAccounts.set(accounts);
          if (accounts.length > 0) {
            this.bankAccountId.set(accounts[0].id);
          }
        },
      });
    } else {
      this.bankAccounts.set([]);
    }
  }


  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      this.fileName.set(file.name);

      const reader = new FileReader();
      reader.onload = () => {
        this.fileContent.set(reader.result as string);
      };
      reader.readAsText(file);
    }
  }

  importStatement(): void {
    if (!this.fileContent() || !this.bankAccountId()) return;

    this.importing.set(true);
    this.result.set(null);

    const request$ = this.format() === 'mt940'
      ? this.bankStatementImportService.importFromMt940({
          companyId: this.companyId(),
          bankAccountId: this.bankAccountId(),
          mt940Content: this.fileContent(),
        } as any)
      : this.bankStatementImportService.importFromCsv({
          companyId: this.companyId(),
          bankAccountId: this.bankAccountId(),
          csvContent: this.fileContent(),
        } as any);

    request$.subscribe({
      next: (res: any) => {
        this.result.set(res);
        this.importing.set(false);
        this.toaster.success(`Imported ${res.importedCount} transactions`);
      },
      error: (err: any) => {
        this.result.set({ importedCount: 0, skippedCount: 0, errors: [err?.error?.error?.message ?? 'Import failed'] });
        this.importing.set(false);
      },
    });
  }
}
