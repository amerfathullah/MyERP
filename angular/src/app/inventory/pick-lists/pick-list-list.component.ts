import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PickListService } from '../../proxy/inventory/pick-list.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-pick-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LocalizationPipe, StatusBadgeComponent, PaginationComponent],
  template: `
    <div class="container-fluid py-3">
      <div class="card shadow-sm">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-clipboard-list me-2"></i>{{ '::PickLists' | abpLocalization }}</h5>
          <a routerLink="/inventory/pick-lists/new" class="btn btn-primary btn-sm">
            <i class="fa fa-plus me-1"></i>{{ '::NewPickList' | abpLocalization }}
          </a>
        </div>

        <div class="card-body border-bottom py-2">
          <div class="row g-2">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm"
                [placeholder]="'::Search' | abpLocalization" (keyup.enter)="loadData()">
            </div>
            <div class="col-md-3">
              <select class="form-select form-select-sm" [(ngModel)]="statusFilter" (change)="loadData()">
                <option value="">{{ '::AllStatuses' | abpLocalization }}</option>
                <option value="Draft">Draft</option>
                <option value="Submitted">Submitted</option>
                <option value="Cancelled">Cancelled</option>
              </select>
            </div>
          </div>
        </div>

        <div class="card-body p-0">
          @if (items().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-clipboard-list fa-2x mb-2"></i>
              <p>{{ '::NoPickListsYet' | abpLocalization }}</p>
              <a routerLink="/inventory/pick-lists/new" class="btn btn-outline-primary btn-sm">
                {{ '::CreatePickList' | abpLocalization }}
              </a>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::Number' | abpLocalization }}</th>
                  <th>{{ '::Purpose' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Items' | abpLocalization }}</th>
                  <th>{{ '::TransferStatus' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td><a [routerLink]="['/inventory/pick-lists', item.id]">{{ item.pickListNumber || '—' }}</a></td>
                    <td>{{ item.purpose || 'Delivery' }}</td>
                    <td><app-status-badge [status]="item.status || 'Draft'" /></td>
                    <td class="text-end">{{ item.items?.length || 0 }}</td>
                    <td>
                      @if (item.isFullyTransferred) {
                        <span class="badge bg-success">{{ '::FullyTransferred' | abpLocalization }}</span>
                      } @else if (item.isPartiallyTransferred) {
                        <span class="badge bg-warning">{{ '::PartiallyTransferred' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-light text-dark">{{ '::NotTransferred' | abpLocalization }}</span>
                      }
                    </td>
                    <td class="text-end">
                      @if (item.status === 'Draft') {
                        <button class="btn btn-outline-success btn-sm me-1" (click)="submitItem(item.id)" title="Submit">
                          <i class="fa fa-paper-plane"></i>
                        </button>
                      }
                      @if (item.status === 'Submitted' && !item.isFullyTransferred) {
                        <button class="btn btn-outline-primary btn-sm" (click)="createDn(item.id)" title="Create Delivery Note">
                          <i class="fa fa-truck"></i>
                        </button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
        @if (totalCount() > 20) {
          <div class="card-footer">
            <app-pagination [totalCount]="totalCount()" [pageSize]="20" [currentPage]="currentPage"
              (pageChange)="onPageChange($event)" />
          </div>
        }
      </div>
    </div>
  `,
})
export class PickListListComponent implements OnInit {
  private service = inject(PickListService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  items = signal<any[]>([]);
  totalCount = signal(0);
  currentPage = 0;
  searchTerm = '';
  statusFilter = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    this.service.getList({
      skipCount: this.currentPage * 20,
      maxResultCount: 20,
      filter: this.searchTerm || undefined,
      status: this.statusFilter || undefined,
      companyId: this.companyContext.currentCompanyId() || undefined,
    } as any).subscribe({
      next: (res) => {
        this.items.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
      },
      error: () => {},
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  submitItem(id: string) {
    this.service.submit(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }

  createDn(id: string) {
    this.service.createDeliveryNoteFromPickList(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCreated'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed'),
    });
  }
}
