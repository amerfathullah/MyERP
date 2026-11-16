import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ListService, PagedResultDto, LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { CompanyService } from '../proxy/core/company.service';
import type { CompanyDto } from '../proxy/core/models';
import { PaginationComponent, type PageEvent } from '../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-company-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    LocalizationPipe,
    PageModule,
    StatusBadgeComponent,
    PaginationComponent],
  providers: [ListService],
  templateUrl: './company-list.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./company-list.component.scss'],
})
export class CompanyListComponent implements OnInit {
  private companyService = inject(CompanyService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  readonly list = inject(ListService);
  companies: CompanyDto[] = [];
  totalCount = 0;
  isLoading = false;
  repairingCompanyId = signal<string | null>(null);
  searchTerm = '';
  pageSize = 10;
  currentPage = 0;

  get filteredCompanies(): CompanyDto[] {
    const term = this.searchTerm.toLowerCase().trim();
    if (!term) return this.companies;
    return this.companies.filter(c =>
      (c.name ?? '').toLowerCase().includes(term) ||
      (c.taxId ?? '').toLowerCase().includes(term) ||
      (c.sstRegistrationNumber ?? '').toLowerCase().includes(term)
    );
  }

  ngOnInit(): void {
    this.isLoading = true;
    const streamCreator = (query: any) => this.companyService.getList({ skipCount: query.skipCount, maxResultCount: query.maxResultCount, sorting: '' });
    this.list.hookToQuery(streamCreator).subscribe((res) => {
      this.companies = res.items ?? [];
      this.totalCount = res.totalCount ?? 0;
      this.isLoading = false;
    });
  }

  private router = inject(Router);

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.list.page = event.pageIndex;
    this.list.maxResultCount = this.pageSize;
    this.list.get();
  }

  createCompany(): void {
    this.router.navigate(['/companies/new']);
  }

  repairAccounts(company: CompanyDto): void {
    if (!company.id) return;
    this.confirmation.warn('::RepairAccountsConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.repairingCompanyId.set(company.id!);
      this.companyService.repairSrbnbAndTaxPayableAccounts(company.id!).subscribe({
        next: () => {
          this.repairingCompanyId.set(null);
          this.toaster.success('::AccountsRepaired');
        },
        error: (err: any) => {
          this.repairingCompanyId.set(null);
          this.toaster.error(err?.error?.error?.message ?? '::OperationFailed');
        },
      });
    });
  }
}
