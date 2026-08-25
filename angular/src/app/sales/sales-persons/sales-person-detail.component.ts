import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { SalesPersonService } from '../../proxy/sales/sales-person.service';

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
              @if (person.isEnabled) {
                <div class="mt-2">
                  <button class="btn btn-outline-danger btn-sm" (click)="disable()">
                    <i class="fa fa-ban me-1"></i>{{ 'Disable' | abpLocalization }}
                  </button>
                </div>
              }
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
                  <tr><td colspan="3" class="text-center text-muted py-3">{{ '::NoTargetsSet' | abpLocalization }}</td></tr>
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
  private service = inject(SalesPersonService);
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  person: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.load(id);
    }
  }

  private load(id: string): void {
    this.isLoading = true;
    this.service.get(id).subscribe({
      next: p => { this.person = p; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  disable(): void {
    this.confirmation.warn('::DisableSalesPersonConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.disable(this.person.id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDisabled'); this.load(this.person.id); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    });
  }
}
