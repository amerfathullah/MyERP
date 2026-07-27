import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProspectService, ProspectDto } from '../../proxy/crm/prospect.service';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  standalone: true,
  selector: 'app-prospect-list',
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-building me-2"></i>{{ 'Prospects' | abpLocalization }}</h5>
          <a routerLink="new" class="btn btn-primary btn-sm">
            <i class="fas fa-plus me-1"></i>{{ 'NewProspect' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          <div class="row mb-3">
            <div class="col-md-4">
              <input type="text" class="form-control form-control-sm" [(ngModel)]="searchTerm"
                     [placeholder]="'::Search' | abpLocalization" (keyup.enter)="loadData()">
            </div>
          </div>
          @if (prospects().length === 0) {
            <div class="text-center py-4 text-muted">
              <i class="fas fa-building fa-2x mb-2"></i>
              <p>{{ 'NoProspectsYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'ProspectName' | abpLocalization }}</th>
                  <th>{{ 'Industry' | abpLocalization }}</th>
                  <th>{{ 'LeadCount' | abpLocalization }}</th>
                  <th>{{ 'OpportunityCount' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of prospects(); track item.id) {
                  <tr>
                    <td>
                      <a [routerLink]="[item.id]">{{ item.prospectName }}</a>
                    </td>
                    <td>{{ item.industry || '—' }}</td>
                    <td><span class="badge bg-info">{{ item.leadCount }}</span></td>
                    <td><span class="badge bg-warning">{{ item.opportunityCount }}</span></td>
                    <td>
                      @if (item.isConverted) {
                        <span class="badge bg-success">{{ 'AlreadyConverted' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-primary">{{ 'Active' | abpLocalization }}</span>
                      }
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
export class ProspectListComponent implements OnInit {
  private service = inject(ProspectService);
  prospects = signal<ProspectDto[]>([]);
  totalCount = signal(0);
  currentPage = signal(0);
  searchTerm = '';

  ngOnInit() { this.loadData(); }

  loadData() {
    this.service.getList({ skipCount: this.currentPage() * 10, maxResultCount: 10, filter: this.searchTerm || undefined }).subscribe({
      next: res => {
        this.prospects.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
      },
      error: () => {}
    });
  }

  onPageChange(e: PageEvent) {
    this.currentPage.set(e.pageIndex);
    this.loadData();
  }
}
