import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListService, PagedResultDto } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';

@Component({
  selector: 'app-company-list',
  standalone: true,
  imports: [CommonModule, PageModule],
  providers: [ListService],
  template: `
    <abp-page [title]="'Companies'">
      <div class="card">
        <div class="card-header d-flex justify-content-between">
          <h5>Companies</h5>
          <button class="btn btn-primary btn-sm" (click)="createCompany()">
            <i class="fa fa-plus me-1"></i> New Company
          </button>
        </div>
        <div class="card-body">
          <table class="table table-striped">
            <thead>
              <tr>
                <th>Name</th>
                <th>TIN</th>
                <th>SST Reg.</th>
                <th>Currency</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let company of companies">
                <td>{{ company.name }}</td>
                <td>{{ company.taxId }}</td>
                <td>{{ company.sstRegistrationNumber }}</td>
                <td>{{ company.currencyCode }}</td>
                <td>
                  <span class="badge" [class.bg-success]="company.isActive" [class.bg-secondary]="!company.isActive">
                    {{ company.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
          <p *ngIf="companies.length === 0" class="text-muted text-center py-4">
            No companies yet. Click "New Company" to get started.
          </p>
        </div>
      </div>
    </abp-page>
  `,
})
export class CompanyListComponent implements OnInit {
  companies: any[] = [];

  constructor(public readonly list: ListService) {}

  ngOnInit(): void {
    // TODO: Wire up to CompanyAppService proxy once ABP proxy generation is run
  }

  createCompany(): void {
    // TODO: Open create dialog
  }
}
