import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ListService, PagedResultDto, LocalizationModule } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { CompanyService } from '../proxy/core/company.service';
import type { CompanyDto } from '../proxy/core/models';

@Component({
  selector: 'app-company-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    LocalizationModule,
    PageModule,
    MatTableModule,
    MatTooltipModule,
    StatusBadgeComponent,
  ],
  providers: [ListService],
  templateUrl: './company-list.component.html',
  styleUrls: ['./company-list.component.scss'],
})
export class CompanyListComponent implements OnInit {
  private companyService = inject(CompanyService);
  companies: CompanyDto[] = [];
  isLoading = false;
  displayedColumns = ['name', 'taxId', 'sstRegistrationNumber', 'currencyCode', 'status', 'actions'];

  constructor(public readonly list: ListService) {}

  ngOnInit(): void {
    this.isLoading = true;
    const streamCreator = (query: any) => this.companyService.getList({ skipCount: query.skipCount, maxResultCount: query.maxResultCount, sorting: '' });
    this.list.hookToQuery(streamCreator).subscribe((res) => {
      this.companies = res.items ?? [];
      this.isLoading = false;
    });
  }

  createCompany(): void {
    // TODO: Open create dialog
  }
}
