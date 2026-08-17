import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ServiceLevelAgreementService } from '../../proxy/support/service-level-agreement.service';
import type { ServiceLevelAgreementDto } from '../../proxy/support/models';

@Component({
  selector: 'app-service-level-agreement-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::ServiceLevelAgreements' | abpLocalization">
      <div class="d-flex justify-content-between gap-2 mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="load()" [placeholder]="'::Placeholder:Search' | abpLocalization">
        <button class="btn btn-primary btn-sm" routerLink="/support/service-level-agreements/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead>
            <tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ '::Scope' | abpLocalization }}</th>
              <th>{{ '::ResponseTimeHours' | abpLocalization }}</th>
              <th>{{ '::ResolutionTimeHours' | abpLocalization }}</th>
              <th>{{ '::Default' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.name }}</td>
                <td>{{ item.entityType ? item.entityType : ('::CompanyDefault' | abpLocalization) }}</td>
                <td>{{ item.responseTimeHours }}h</td>
                <td>{{ item.resolutionTimeHours }}h</td>
                <td>@if (item.isDefault) { <span class="badge bg-success">{{ '::Default' | abpLocalization }}</span> }</td>
                <td>
                  <div class="btn-group btn-group-sm">
                    <a class="btn btn-outline-primary" [routerLink]="'/support/service-level-agreements/' + item.id"><i class="fa fa-edit"></i></a>
                    <button class="btn btn-outline-danger" (click)="delete(item)"><i class="fa fa-trash"></i></button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class ServiceLevelAgreementListComponent implements OnInit {
  private service = inject(ServiceLevelAgreementService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<ServiceLevelAgreementDto[]>([]);
  searchTerm = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name', filter: this.searchTerm } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  delete(item: ServiceLevelAgreementDto): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        this.service.delete(item.id!).subscribe({
          next: () => { this.toaster.success('::SuccessfullyDeleted'); this.load(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Delete failed'),
        });
      }
    });
  }
}
