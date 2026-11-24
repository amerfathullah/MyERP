import { Component, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe } from '@abp/ng.core';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { PricingRuleService } from '../../proxy/sales/pricing-rule.service';

@Component({
  selector: 'app-pricing-rule-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, BreadcrumbComponent, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <app-breadcrumb />
    <abp-page [title]="rule?.title ?? ('::PricingRule' | abpLocalization)">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (rule) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::ApplyOn' | abpLocalization }}</div>
              <div class="fs-5 fw-bold">{{ rule.applyOn }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::RuleType' | abpLocalization }}</div>
              <div class="fs-5 fw-bold">{{ rule.ruleType }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::DiscountOrRate' | abpLocalization }}</div>
              <div class="fs-4 fw-bold text-primary">
                @if (rule.ruleType === 'Discount' || rule.ruleType === 0) {
                  {{ rule.discountPercentage > 0 ? rule.discountPercentage + '%' : (rule.discountAmount | number:'1.2-2') }}
                } @else if (rule.ruleType === 'Rate' || rule.ruleType === 1) {
                  {{ rule.rate | number:'1.2-2' }}
                } @else {
                  {{ '::FreeItem' | abpLocalization }}
                }
              </div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ '::Priority' | abpLocalization }}</div>
              <div class="fs-4 fw-bold">{{ rule.priority }}</div>
              <span class="badge" [class]="rule.isEnabled ? 'bg-success' : 'bg-secondary'">
                {{ (rule.isEnabled ? '::Active' : '::Disabled') | abpLocalization }}
              </span>
            </div></div>
          </div>
        </div>

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">{{ '::Configuration' | abpLocalization }}</h6></div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-6">
                <dl class="mb-0">
                  <dt>{{ '::ApplicableFor' | abpLocalization }}</dt>
                  <dd>{{ rule.applicableFor ?? ('::Both' | abpLocalization) }}</dd>
                  @if (rule.minQty > 0) { <dt>{{ '::MinQty' | abpLocalization }}</dt><dd>{{ rule.minQty }}</dd> }
                  @if (rule.maxQty > 0) { <dt>{{ '::MaxQty' | abpLocalization }}</dt><dd>{{ rule.maxQty }}</dd> }
                  @if (rule.minAmount > 0) { <dt>{{ '::MinAmount' | abpLocalization }}</dt><dd>{{ rule.minAmount | number:'1.2-2' }}</dd> }
                  @if (rule.maxAmount > 0) { <dt>{{ '::MaxAmount' | abpLocalization }}</dt><dd>{{ rule.maxAmount | number:'1.2-2' }}</dd> }
                </dl>
              </div>
              <div class="col-md-6">
                <dl class="mb-0">
                  @if (rule.validFrom) { <dt>{{ '::ValidFrom' | abpLocalization }}</dt><dd>{{ rule.validFrom | date:'dd/MM/yyyy' }}</dd> }
                  @if (rule.validUpto) { <dt>{{ '::ValidUntil' | abpLocalization }}</dt><dd>{{ rule.validUpto | date:'dd/MM/yyyy' }}</dd> }
                  @if (rule.itemCode) { <dt>{{ '::ItemCode' | abpLocalization }}</dt><dd>{{ rule.itemCode }}</dd> }
                  @if (rule.itemGroup) { <dt>{{ '::ItemGroup' | abpLocalization }}</dt><dd>{{ rule.itemGroup }}</dd> }
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
  private service = inject(PricingRuleService);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  rule: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.service.get(id).subscribe({
        next: r => { this.rule = r; this.isLoading = false; },
        error: (err: any) => {
          this.isLoading = false;
          this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        }
      });
    }
  }
}
