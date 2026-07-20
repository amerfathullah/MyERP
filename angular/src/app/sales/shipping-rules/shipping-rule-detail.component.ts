import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ShippingRuleService } from '../../proxy/sales/shipping-rule.service';

@Component({
  selector: 'app-shipping-rule-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="rule?.name ?? 'Shipping Rule'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (rule) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Type' | abpLocalization }}</div>
              <div class="fs-5 fw-bold">{{ rule.shippingRuleType }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Calculation Mode</div>
              <div class="fs-5 fw-bold">{{ rule.calculationMode }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'FixedAmount' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ rule.fixedAmount | number:'1.2-2' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <span class="badge fs-6" [class]="rule.isEnabled ? 'bg-success' : 'bg-secondary'">
                {{ rule.isEnabled ? 'Active' : 'Disabled' }}
              </span>
            </div></div>
          </div>
        </div>

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">Conditions</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr><th>From</th><th>To</th><th class="text-end">Amount</th></tr></thead>
              <tbody>
                @for (c of rule.conditions; track $index) {
                  <tr>
                    <td>{{ c.fromValue | number:'1.2-2' }}</td>
                    <td>{{ c.toValue > 0 ? (c.toValue | number:'1.2-2') : 'Any (catch-all)' }}</td>
                    <td class="text-end fw-bold">{{ c.shippingAmount | number:'1.2-2' }}</td>
                  </tr>
                }
                @empty {
                  <tr><td colspan="3" class="text-center text-muted py-3">Fixed amount (no conditions)</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        @if (rule.countries?.length) {
          <div class="card"><div class="card-header"><h6 class="mb-0">Country Restrictions</h6></div>
            <div class="card-body">
              @for (c of rule.countries; track c.countryCode) {
                <span class="badge bg-light text-dark me-1 mb-1">{{ c.countryCode }}</span>
              }
            </div>
          </div>
        }
      }
    </abp-page>
  `
})
export class ShippingRuleDetailComponent implements OnInit {
  private service = inject(ShippingRuleService);
  private route = inject(ActivatedRoute);
  rule: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.service.get(id).subscribe({
        next: r => { this.rule = r; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
