import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { IssueDetailService } from '../../shared/services/detail-services';
import type { IssueDto } from '../../proxy/support/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-issue-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'Issues' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <div class="card"><div class="card-body">
          <h5>{{ d.subject }}</h5>
          <div class="row mt-3">
            <div class="col-md-3"><strong>{{ 'Priority' | abpLocalization }}:</strong> {{ d.priority }}</div>
            <div class="col-md-3"><strong>{{ 'Status' | abpLocalization }}:</strong> <span class="badge bg-info">{{ ['Open','Replied','On Hold','Closed','Cancelled'][d.status ?? 0] }}</span></div>
            <div class="col-md-3"><strong>{{ 'Type' | abpLocalization }}:</strong> {{ d.issueType ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'Date' | abpLocalization }}:</strong> {{ d.creationTime | date:'dd/MM/yyyy' }}</div>
          </div>
          @if (d.serviceLevelAgreementId) {
            <div class="row mt-2">
              <div class="col-md-3"><strong>{{ '::AgreementStatus' | abpLocalization }}:</strong> <span [class]="agreementStatusClass(d.agreementStatus)">{{ agreementStatusLabel(d.agreementStatus) }}</span></div>
              <div class="col-md-3"><strong>{{ '::FirstResponseTime' | abpLocalization }}:</strong> {{ d.firstResponseTime ?? '—' }}h</div>
              <div class="col-md-3"><strong>{{ '::ResolutionTime' | abpLocalization }}:</strong> {{ d.resolutionTime ?? '—' }}h</div>
            </div>
            <div class="row mt-2">
              <div class="col-md-3"><strong>{{ '::RespondBy' | abpLocalization }}:</strong> {{ d.responseByDate ? (d.responseByDate | date:'dd/MM/yyyy HH:mm') : '—' }}</div>
              <div class="col-md-3"><strong>{{ '::ResolveBy' | abpLocalization }}:</strong> {{ d.resolutionByDate ? (d.resolutionByDate | date:'dd/MM/yyyy HH:mm') : '—' }}</div>
            </div>
          }
          @if (d.description) { <div class="mt-3"><strong>{{ 'Description' | abpLocalization }}:</strong><p class="mt-1">{{ d.description }}</p></div> }
          <div class="mt-3 d-flex gap-2">
            @if (d.status === 0) { <button class="btn btn-sm btn-success" (click)="action('reply')"><i class="fa fa-reply me-1"></i>Reply</button> }
            @if ((d.status ?? 0) < 3) { <button class="btn btn-sm btn-primary" (click)="action('resolve')"><i class="fa fa-check me-1"></i>Resolve</button> }
          </div>
        </div></div>
      }
    </abp-page>
  `,
})
export class IssueDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(IssueDetailService);
  d: IssueDto | null = null;
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }
  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    if (type === 'reply') this.service.reply(id).subscribe({ next: () => this.ngOnInit(), error: () => {} });
    else if (type === 'resolve') this.service.resolve(id).subscribe({ next: () => this.ngOnInit(), error: () => {} });
  }

  agreementStatusLabel(status: number | undefined): string {
    return ['First Response Due', 'Resolution Due', 'Fulfilled', 'Failed', 'Paused'][status ?? 0] ?? '—';
  }

  agreementStatusClass(status: number | undefined): string {
    switch (status) {
      case 2: return 'badge bg-success';
      case 3: return 'badge bg-danger';
      case 4: return 'badge bg-secondary';
      default: return 'badge bg-warning text-dark';
    }
  }
}
