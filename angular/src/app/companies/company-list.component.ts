import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { ListService, PagedResultDto, LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { CompanyService } from '../proxy/core/company.service';
import type { CompanyDto } from '../proxy/core/models';
import { PaginationComponent, type PageEvent } from '../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-company-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    LocalizationPipe,
    PageModule,
    StatusBadgeComponent,
    PaginationComponent],
  providers: [ListService],
  templateUrl: './company-list.component.html',
  styleUrls: ['./company-list.component.scss'],
})
export class CompanyListComponent implements OnInit {
  private companyService = inject(CompanyService);
  readonly list = inject(ListService);
  companies: CompanyDto[] = [];
  totalCount = 0;
  isLoading = false;
  pageSize = 10;
  currentPage = 0;

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
}
