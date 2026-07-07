import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ListService } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [CommonModule, PageModule],
  providers: [ListService],
  template: `
    <abp-page [title]="'Customers'">
      <div class="card">
        <div class="card-header d-flex justify-content-between">
          <h5>Customers</h5>
          <button class="btn btn-primary btn-sm" (click)="createCustomer()">
            <i class="fa fa-plus me-1"></i> New Customer
          </button>
        </div>
        <div class="card-body">
          <table class="table table-striped">
            <thead>
              <tr>
                <th>Name</th>
                <th>Code</th>
                <th>TIN</th>
                <th>Phone</th>
                <th>Email</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let customer of customers">
                <td>{{ customer.name }}</td>
                <td>{{ customer.customerCode }}</td>
                <td>{{ customer.tin }}</td>
                <td>{{ customer.phone }}</td>
                <td>{{ customer.email }}</td>
                <td>
                  <span class="badge" [class.bg-success]="customer.isActive" [class.bg-secondary]="!customer.isActive">
                    {{ customer.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </abp-page>
  `,
})
export class CustomerListComponent implements OnInit {
  customers: any[] = [];

  constructor(public readonly list: ListService) {}

  ngOnInit(): void {
    // TODO: Wire up to CustomerAppService proxy
  }

  createCustomer(): void {
    // TODO: Open create dialog
  }
}
