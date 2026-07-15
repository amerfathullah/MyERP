import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-sales-person-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="person?.name ?? 'Sales Person'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (person) {
        <div class="row g-3 mb-4">
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Commission Rate</div>
              <div class="fs-3 fw-bold text-primary">{{ person.commissionRate }}%</div>
            </div></div>
          </div>
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Type' | abpLocalization }}</div>
              <div class="fs-5 fw-bold">{{ person.isGroup ? 'Group (Manager)' : 'Individual' }}</div>
            </div></div>
          </div>
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <span class="badge fs-6" [class]="person.isEnabled ? 'bg-success' : 'bg-secondary'">
                {{ person.isEnabled ? 'Active' : 'Disabled' }}
              </span>
            </div></div>
          </div>
        </div>

        @if (person.parentSalesPersonId) {
          <div class="alert alert-light">
            <i class="fa fa-sitemap me-2"></i>Reports to: <strong>{{ person.parentSalesPersonName ?? person.parentSalesPersonId }}</strong>
          </div>
        }

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">Targets</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>Fiscal Year</th>
                <th class="text-end">Target Qty</th>
                <th class="text-end">Target Amount</th>
              </tr></thead>
              <tbody>
                @for (t of person.targets; track t.fiscalYear) {
                  <tr>
                    <td>{{ t.fiscalYear }}</td>
                    <td class="text-end">{{ t.targetQty | number:'1.0-0' }}</td>
                    <td class="text-end">{{ t.targetAmount | number:'1.2-2' }}</td>
                  </tr>
                }
                @empty {
                  <tr><td colspan="3" class="text-center text-muted py-3">No targets set</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class SalesPersonDetailComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  person: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.http.get<any>(`/api/app/sales-person/${id}`).subscribe({
        next: p => { this.person = p; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
