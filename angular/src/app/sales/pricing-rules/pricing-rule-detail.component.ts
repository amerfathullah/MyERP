import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { RestService } from '@abp/ng.core';

@Component({
  selector: 'app-pricing-rule-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="rule?.title ?? 'Pricing Rule'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (rule) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Apply On</div>
              <div class="fs-5 fw-bold">{{ rule.applyOn }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Rule Type</div>
              <div class="fs-5 fw-bold">{{ rule.ruleType }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Discount / Rate</div>
              <div class="fs-4 fw-bold text-primary">
                @if (rule.ruleType === 'Discount' || rule.ruleType === 0) {
                  {{ rule.discountPercentage > 0 ? rule.discountPercentage + '%' : (rule.discountAmount | number:'1.2-2') }}
                } @else if (rule.ruleType === 'Rate' || rule.ruleType === 1) {
                  {{ rule.rate | number:'1.2-2' }}
                } @else {
                  Free Item
                }
              </div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Priority</div>
              <div class="fs-4 fw-bold">{{ rule.priority }}</div>
              <span class="badge" [class]="rule.isEnabled ? 'bg-success' : 'bg-secondary'">
                {{ rule.isEnabled ? 'Active' : 'Disabled' }}
              </span>
            </div></div>
          </div>
        </div>

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">Configuration</h6></div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-6">
                <dl class="mb-0">
                  <dt>Applicable For</dt>
                  <dd>{{ rule.applicableFor ?? 'Both' }}</dd>
                  @if (rule.minQty > 0) { <dt>Min Qty</dt><dd>{{ rule.minQty }}</dd> }
                  @if (rule.maxQty > 0) { <dt>Max Qty</dt><dd>{{ rule.maxQty }}</dd> }
                  @if (rule.minAmount > 0) { <dt>Min Amount</dt><dd>{{ rule.minAmount | number:'1.2-2' }}</dd> }
                  @if (rule.maxAmount > 0) { <dt>Max Amount</dt><dd>{{ rule.maxAmount | number:'1.2-2' }}</dd> }
                </dl>
              </div>
              <div class="col-md-6">
                <dl class="mb-0">
                  @if (rule.validFrom) { <dt>Valid From</dt><dd>{{ rule.validFrom | date:'dd/MM/yyyy' }}</dd> }
                  @if (rule.validUpto) { <dt>Valid Until</dt><dd>{{ rule.validUpto | date:'dd/MM/yyyy' }}</dd> }
                  @if (rule.itemCode) { <dt>Item Code</dt><dd>{{ rule.itemCode }}</dd> }
                  @if (rule.itemGroup) { <dt>Item Group</dt><dd>{{ rule.itemGroup }}</dd> }
                </dl>
              </div>
            </div>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class PricingRuleDetailComponent implements OnInit {
  private restService = inject(RestService);
  private route = inject(ActivatedRoute);
  rule: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.restService.request<any, any>({ method: 'GET', url: `/api/app/pricing-rule/${id}` }, { apiName: 'Default' }).subscribe({
        next: r => { this.rule = r; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
