import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { OpportunityService } from '../../proxy/crm/opportunity.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-opportunity-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  template: `
    <abp-page [title]="opp()?.opportunityName ?? ('Opportunity' | abpLocalization)">
  <app-breadcrumb />
      @if (isLoading()) { <app-loading-overlay /> }
      @if (opp(); as o) {
        <div class="d-flex justify-content-end gap-2 mb-3">
          @if (o.status === 0 || o.status === 1) {
            <a class="btn btn-outline-primary btn-sm" [routerLink]="'/crm/opportunities/' + o.id + '/edit'">
              <i class="fa fa-edit me-1"></i>{{ 'Edit' | abpLocalization }}
            </a>
          }
        </div>
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-4 mb-3">
                <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
                <app-status-badge [status]="getStatus(o.status)" />
              </div>
              <div class="col-md-4 mb-3">
                <small class="text-muted d-block">{{ '::SalesStage' | abpLocalization }}</small>
                <span class="fw-bold">{{ o.salesStage }}</span>
              </div>
              <div class="col-md-4 mb-3">
                <small class="text-muted d-block">{{ '::Amount' | abpLocalization }}</small>
                <span class="fw-bold">{{ o.amount | number:'1.2-2' }} {{ o.currencyCode }}</span>
              </div>
            </div>
            @if (o.probability) {
              <div class="row">
                <div class="col-md-4">
                  <small class="text-muted d-block">Probability</small>
                  <div class="d-flex align-items-center gap-2">
                    <div class="progress flex-grow-1" style="height: 8px;">
                      <div class="progress-bar" [style.width.%]="o.probability"></div>
                    </div>
                    <small>{{ o.probability }}%</small>
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
        @if (o.items && o.items.length > 0) {
          <div class="card">
            <div class="card-header fw-bold">{{ '::Items' | abpLocalization }}</div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>{{ '::Item' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Qty' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Rate' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Amount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of o.items; track item.id) {
                    <tr>
                      <td>{{ item.itemName }}</td>
                      <td class="text-end">{{ item.qty }}</td>
                      <td class="text-end">{{ item.rate | number:'1.2-2' }}</td>
                      <td class="text-end">{{ item.amount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      }
    </abp-page>
  `,
})
export class OpportunityDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(OpportunityService);

  opp = signal<any>(null);
  isLoading = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading.set(true);
      this.service.get(id).subscribe({
        next: o => { this.opp.set(o); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
    }
  }

  getStatus(s: number | undefined): string {
    return ['Open', 'Replied', 'Quotation', 'Converted', 'Lost', 'Closed'][s ?? 0] ?? 'Open';
  }
}
