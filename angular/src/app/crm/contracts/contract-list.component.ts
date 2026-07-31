import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { ContractService } from '../../proxy/crm/contract.service';
import type { ContractDto } from '../../proxy/crm/models';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  standalone: true,
  selector: 'app-contract-list',
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-file-contract me-2"></i>{{ 'Contracts' | abpLocalization }}</h5>
          <a routerLink="new" class="btn btn-primary btn-sm">
            <i class="fas fa-plus me-1"></i>{{ 'NewContract' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm"
                     [placeholder]="'::Search' | abpLocalization" (keyup.enter)="loadData()">
            </div>
          </div>
          @if (contracts().length === 0) {
            <div class="text-center py-4 text-muted">
              <i class="fas fa-file-contract fa-2x mb-2"></i>
              <p>{{ 'NoContractsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'ContractNumber' | abpLocalization }}</th>
                  <th>{{ 'PartyType' | abpLocalization }}</th>
                  <th>{{ 'StartDate' | abpLocalization }}</th>
                  <th>{{ 'EndDate' | abpLocalization }}</th>
                  <th>{{ 'ContractValue' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (item of contracts(); track item.id) {
                  <tr>
                    <td><a [routerLink]="[item.id]">{{ item.contractNumber }}</a></td>
                    <td>{{ item.partyType }}</td>
                    <td>{{ item.startDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ item.endDate ? (item.endDate | date:'dd/MM/yyyy') : '—' }}</td>
                    <td>{{ item.contractValue ? (item.contractValue | number:'1.2-2') : '—' }}</td>
                    <td>
                      @switch (item.status) {
                        @case (0) { <span class="badge bg-secondary">{{ 'Unsigned' | abpLocalization }}</span> }
                        @case (1) { <span class="badge bg-success">{{ 'Active' | abpLocalization }}</span> }
                        @case (2) { <span class="badge bg-warning">{{ 'InactiveByExpiry' | abpLocalization }}</span> }
                        @case (3) { <span class="badge bg-danger">{{ 'InactiveByAutoRenewFailure' | abpLocalization }}</span> }
                        @case (4) { <span class="badge bg-dark">{{ 'Cancelled' | abpLocalization }}</span> }
                      }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        @if (item.status === 0) {
                          <button class="btn btn-outline-success btn-sm" (click)="sign(item.id!)">
                            <i class="fas fa-signature"></i>
                          </button>
                        }
                        @if (item.status !== 4) {
                          <button class="btn btn-outline-danger btn-sm" (click)="cancel(item.id!)">
                            <i class="fas fa-times"></i>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage()"
                            (pageChange)="onPageChange($event)"></app-pagination>
          }
        </div>
      </div>
    </div>
  `
})
export class ContractListComponent implements OnInit {
  private service = inject(ContractService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  contracts = signal<ContractDto[]>([]);
  totalCount = signal(0);
  currentPage = signal(0);
  searchTerm = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    this.service.getList({ skipCount: this.currentPage() * 10, maxResultCount: 10, filter: this.searchTerm || undefined }).subscribe({
      next: res => {
        this.contracts.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
      error: () => {}
    });
  }

  sign(id: string) {
    this.service.sign(id).subscribe({
      next: () => { this.toaster.success('MyERP::SuccessfullyUpdated'); this.loadData(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Sign failed'),
    });
  }

  cancel(id: string) {
    this.confirmation.warn('MyERP::CancelConfirmation', 'MyERP::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(id).subscribe({
        next: () => { this.toaster.success('MyERP::SuccessfullyCancelled'); this.loadData(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Cancel failed'),
      });
    });
  }

  onPageChange(e: PageEvent) {
    this.currentPage.set(e.pageIndex);
    this.loadData();
  }
}
