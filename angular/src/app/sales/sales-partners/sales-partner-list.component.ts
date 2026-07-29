import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ToasterService , ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-sales-partner-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SalesPartners' | abpLocalization">
      <div class="d-flex justify-content-between mb-3">
        <input type="text" class="form-control form-control-sm" style="max-width: 300px"
          [placeholder]="'::Placeholder:Search' | abpLocalization"
          [(ngModel)]="searchTerm" (keyup.enter)="onSearch()">
        <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
          <i class="fa fa-plus me-1"></i>{{ 'NewSalesPartner' | abpLocalization }}
        </button>
      </div>

      @if (showCreateForm) {
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'NewSalesPartner' | abpLocalization }}</h6>
          <div class="row g-2">
            <div class="col-md-4">
              <label class="form-label">{{ 'Name' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="newPartner.name">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'Type' | abpLocalization }}</label>
              <select class="form-select form-select-sm" [(ngModel)]="newPartner.partnerType">
                <option [value]="0">Reseller</option>
                <option [value]="1">Distributor</option>
                <option [value]="2">Dealer</option>
                <option [value]="3">Agent</option>
                <option [value]="4">Broker</option>
                <option [value]="5">Referral</option>
              </select>
            </div>
            <div class="col-md-2">
              <label class="form-label">{{ 'CommissionRate' | abpLocalization }} (%)</label>
              <input type="number" class="form-control form-control-sm" min="0" max="100" step="0.01"
                [(ngModel)]="newPartner.commissionRate">
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'Website' | abpLocalization }}</label>
              <input type="text" class="form-control form-control-sm" [(ngModel)]="newPartner.website">
            </div>
          </div>
          <div class="mt-2">
            <button class="btn btn-success btn-sm me-2" (click)="create()">
              <i class="fa fa-check me-1"></i>{{ 'Save' | abpLocalization }}
            </button>
            <button class="btn btn-outline-secondary btn-sm" (click)="showCreateForm = false">
              {{ 'Cancel' | abpLocalization }}
            </button>
          </div>
        </div></div>
      }

      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && partners.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-handshake fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSalesPartnersYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th>{{ 'CommissionRate' | abpLocalization }}</th>
              <th>{{ 'Website' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (p of partners; track p.id) {
                <tr>
                  <td><a [routerLink]="[p.id]" class="text-decoration-none"><strong>{{ p.name }}</strong></a></td>
                  <td><span class="badge bg-info">{{ getPartnerTypeName(p.partnerType) }}</span></td>
                  <td>{{ p.commissionRate }}%</td>
                  <td>
                    @if (p.website) {
                      <a [href]="p.website" target="_blank" class="text-primary">
                        <i class="fa fa-external-link-alt me-1"></i>{{ p.website | slice:0:30 }}
                      </a>
                    }
                  </td>
                  <td>
                    <span class="badge" [class]="p.isEnabled ? 'bg-success' : 'bg-secondary'">
                      {{ p.isEnabled ? 'Active' : 'Disabled' }}
                    </span>
                  </td>
                  <td>
                    <button class="btn btn-outline-secondary btn-sm me-1" (click)="toggle(p)">
                      <i class="fa" [class]="p.isEnabled ? 'fa-ban' : 'fa-check'"></i>
                    </button>
                    <button class="btn btn-outline-danger btn-sm" (click)="remove(p)">
                      <i class="fa fa-trash"></i>
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class SalesPartnerListComponent implements OnInit {
  private http = inject(HttpClient);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  partners: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 50;
  currentPage = 0;
  searchTerm = '';
  showCreateForm = false;

  newPartner = { name: '', partnerType: 0, commissionRate: 0, website: '' };

  private partnerTypes = ['Reseller', 'Distributor', 'Dealer', 'Agent', 'Broker', 'Referral'];

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const params: any = { skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize };
    if (this.searchTerm) params.filter = this.searchTerm;
    this.http.get<any>('/api/app/sales-partner', { params }).subscribe({
      next: res => { this.partners = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  onSearch() { this.currentPage = 0; this.loadData(); }
  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
  getPartnerTypeName(type: number) { return this.partnerTypes[type] ?? 'Unknown'; }

  create() {
    this.http.post('/api/app/sales-partner', this.newPartner).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCreated'); this.showCreateForm = false; this.loadData();
        this.newPartner = { name: '', partnerType: 0, commissionRate: 0, website: '' }; },
      error: () => {}
    });
  }

  toggle(p: any) {
    this.http.post(`/api/app/sales-partner/${p.id}/toggle`, {}).subscribe({
      next: () => { this.loadData(); },
      error: () => {}
    });
  }

  remove(p: any) {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.http.delete(`/api/app/sales-partner/${p.id}`).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadData(); },
        error: () => {}
      });
    });
  }
}
