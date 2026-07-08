import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListService, PagedResultDto } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../shared/components/loading-overlay/loading-overlay.component';
import { CompanyService } from '../proxy/core/company.service';
import type { CompanyDto } from '../proxy/core/models';

@Component({
  selector: 'app-company-list',
  standalone: true,
  imports: [
    CommonModule,
    PageModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    StatusBadgeComponent,
    LoadingOverlayComponent,
  ],
  providers: [ListService],
  templateUrl: './company-list.component.html',
  styleUrls: ['./company-list.component.scss'],
})
export class CompanyListComponent implements OnInit {
  private companyService = inject(CompanyService);
  companies: CompanyDto[] = [];
  isLoading = false;
  displayedColumns = ['name', 'taxId', 'sstRegistrationNumber', 'currencyCode', 'status'];

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
