import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { LoyaltyProgramService } from '../../proxy/sales/loyalty-program.service';

@Component({
  selector: 'app-loyalty-program-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="program?.name ?? 'Loyalty Program'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (program) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'ConversionFactor' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ program.conversionFactor }}</div>
              <div class="text-muted small">amount per point</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'ExpiryDays' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ program.expiryDurationDays > 0 ? program.expiryDurationDays : '∞' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Tiers' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ program.tiers?.length ?? 0 }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <span class="badge fs-6" [class]="program.isEnabled ? 'bg-success' : 'bg-secondary'">
                {{ program.isEnabled ? 'Active' : 'Disabled' }}
              </span>
            </div></div>
          </div>
        </div>

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">{{ 'Tiers' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>{{ 'TierName' | abpLocalization }}</th>
                <th class="text-end">Min Spent</th>
                <th class="text-end">Collection Factor</th>
                <th class="text-end">Redemption Factor</th>
              </tr></thead>
              <tbody>
                @for (tier of program.tiers; track tier.name) {
                  <tr>
                    <td><strong>{{ tier.name }}</strong></td>
                    <td class="text-end">{{ tier.minSpent | number:'1.2-2' }}</td>
                    <td class="text-end">{{ tier.collectionFactor }}×</td>
                    <td class="text-end">{{ tier.redemptionFactor }}</td>
                  </tr>
                }
                @empty {
                  <tr><td colspan="4" class="text-center text-muted py-3">No tiers configured</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        @if (program.expenseAccountId) {
          <div class="card"><div class="card-body">
            <small class="text-muted">Expense Account ID: {{ program.expenseAccountId }}</small>
          </div></div>
        }
      }
    </abp-page>
  `
})
export class LoyaltyProgramDetailComponent implements OnInit {
  private service = inject(LoyaltyProgramService);
  private route = inject(ActivatedRoute);
  program: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.service.get(id).subscribe({
        next: p => { this.program = p; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
