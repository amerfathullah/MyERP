import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { RestService } from '@abp/ng.core';

@Component({
  selector: 'app-supplier-scorecard-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="scorecard?.supplierName ?? 'Supplier Scorecard'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (scorecard) {
        <div class="row g-3 mb-4">
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Current Score</div>
              <div class="fs-2 fw-bold" [class]="scorecard.score >= 70 ? 'text-success' : scorecard.score >= 40 ? 'text-warning' : 'text-danger'">
                {{ scorecard.score | number:'1.1-1' }}
              </div>
              <div class="text-muted small">out of 100</div>
            </div></div>
          </div>
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Period Type</div>
              <div class="fs-5 fw-bold">{{ scorecard.periodType }}</div>
            </div></div>
          </div>
          <div class="col-md-4">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Enforcement</div>
              @if (scorecard.preventPurchaseOrders) {
                <span class="badge bg-danger me-1">Blocks PO</span>
              }
              @if (scorecard.preventRfqs) {
                <span class="badge bg-danger me-1">Blocks RFQ</span>
              }
              @if (!scorecard.preventPurchaseOrders && !scorecard.preventRfqs) {
                <span class="badge bg-success">No Restrictions</span>
              }
            </div></div>
          </div>
        </div>

        <div class="card mb-4"><div class="card-header"><h6 class="mb-0">Standing Bands</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr>
                <th>Grade</th>
                <th>Range</th>
                <th>Prevent PO</th>
                <th>Prevent RFQ</th>
              </tr></thead>
              <tbody>
                @for (s of scorecard.standings; track s.name) {
                  <tr [class]="isCurrentStanding(s) ? 'table-active' : ''">
                    <td><strong>{{ s.name }}</strong> @if (isCurrentStanding(s)) { <i class="fa fa-arrow-left ms-1 text-primary"></i> }</td>
                    <td>{{ s.minScore }} – {{ s.maxScore }}</td>
                    <td>@if (s.preventPos) { <i class="fa fa-ban text-danger"></i> } @else { <i class="fa fa-check text-success"></i> }</td>
                    <td>@if (s.preventRfqs) { <i class="fa fa-ban text-danger"></i> } @else { <i class="fa fa-check text-success"></i> }</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="card"><div class="card-header"><h6 class="mb-0">Evaluation Criteria</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead><tr><th>Criteria</th><th class="text-end">Weight</th><th class="text-end">Max Score</th></tr></thead>
              <tbody>
                @for (c of scorecard.criteria; track c.name) {
                  <tr>
                    <td>{{ c.name }}</td>
                    <td class="text-end">{{ c.weight }}%</td>
                    <td class="text-end">{{ c.maxScore }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class SupplierScorecardDetailComponent implements OnInit {
  private restService = inject(RestService);
  private route = inject(ActivatedRoute);
  scorecard: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.restService.request<any, any>({ method: 'GET', url: `/api/app/supplier-scorecard/${id}` }, { apiName: 'Default' }).subscribe({
        next: s => { this.scorecard = s; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }

  isCurrentStanding(standing: any): boolean {
    return this.scorecard?.score >= standing.minScore && this.scorecard?.score <= standing.maxScore;
  }
}
