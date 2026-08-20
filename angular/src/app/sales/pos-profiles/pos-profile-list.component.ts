import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PosProfileService } from '../../proxy/sales/pos-profile.service';
import type { PosProfileDto } from '../../proxy/sales/models';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-pos-profile-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-shop me-2"></i>{{ 'MyERP::PosProfiles' | abpLocalization }}</h5>
        <a routerLink="new" class="btn btn-primary btn-sm">
          <i class="bi bi-plus-lg me-1"></i>{{ 'MyERP::NewPosProfile' | abpLocalization }}
        </a>
      </div>
      <div class="card-body">
        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead class="table-light">
              <tr>
                <th>{{ 'MyERP::ProfileName' | abpLocalization }}</th>
                <th>{{ 'MyERP::InvoiceType' | abpLocalization }}</th>
                <th>{{ 'MyERP::Currency' | abpLocalization }}</th>
                <th class="text-center">{{ 'MyERP::ValidateStock' | abpLocalization }}</th>
                <th class="text-center">{{ 'MyERP::PaymentModes' | abpLocalization }}</th>
                <th class="text-center">{{ 'MyERP::Status' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (profile of profiles(); track profile.id) {
                <tr [class.table-secondary]="profile.isDisabled">
                  <td>
                    <a [routerLink]="[profile.id]" class="text-decoration-none fw-medium">
                      {{ profile.profileName }}
                    </a>
                  </td>
                  <td><span class="badge bg-info">{{ profile.invoiceType }}</span></td>
                  <td>{{ profile.currencyCode }}</td>
                  <td class="text-center">
                    @if (profile.validateStock) { <i class="bi bi-check-circle text-success"></i> }
                    @else { <i class="bi bi-x-circle text-muted"></i> }
                  </td>
                  <td class="text-center">
                    <span class="badge bg-secondary">{{ profile.paymentMethods?.length || 0 }}</span>
                  </td>
                  <td class="text-center">
                    @if (profile.isDisabled) {
                      <span class="badge bg-secondary">Disabled</span>
                    } @else {
                      <span class="badge bg-success">Active</span>
                    }
                  </td>
                  <td class="text-end">
                    <div class="btn-group btn-group-sm">
                      <a [routerLink]="[profile.id, 'edit']" class="btn btn-outline-primary">
                        <i class="bi bi-pencil"></i>
                      </a>
                      @if (profile.isDisabled) {
                        <button class="btn btn-outline-success" (click)="enable(profile.id!)">
                          <i class="bi bi-check-lg"></i>
                        </button>
                      } @else {
                        <button class="btn btn-outline-danger" (click)="disable(profile.id!)">
                          <i class="bi bi-x-lg"></i>
                        </button>
                      }
                    </div>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="7" class="text-center text-muted py-4">
                    {{ 'MyERP::NoRecordsFound' | abpLocalization }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <app-pagination [totalCount]="totalCount()" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      </div>
    </div>
  `,
})
export class PosProfileListComponent implements OnInit {
  private service = inject(PosProfileService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  profiles = signal<PosProfileDto[]>([]);
  totalCount = signal(0);
  currentPage = 0;
  pageSize = 20;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.service.getList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      companyId: this.companyContext.currentCompanyId() || undefined,
    }).subscribe({
      next: (res) => {
        this.profiles.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
    });
  }

  enable(id: string) {
    this.service.enable(id).subscribe({
      next: () => { this.toaster.success('MyERP::SuccessfullyUpdated'); this.loadData(); },
    });
  }

  disable(id: string) {
    this.confirmation.warn('MyERP::AreYouSure', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.disable(id).subscribe({
          next: () => { this.toaster.success('MyERP::SuccessfullyUpdated'); this.loadData(); },
        });
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }
}
