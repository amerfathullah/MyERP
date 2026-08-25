import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { IssueDetailService } from '../../shared/services/detail-services';
import type { IssueDto } from '../../proxy/support/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-issue-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
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
            @if (d.status === 0) { <button class="btn btn-sm btn-success" [disabled]="busy" (click)="action('reply')"><i class="fa fa-reply me-1"></i>Reply</button> }
            @if ((d.status ?? 0) < 3) { <button class="btn btn-sm btn-primary" [disabled]="busy" (click)="action('resolve')"><i class="fa fa-check me-1"></i>Resolve</button> }
            @if (d.status === 0 || d.status === 1) { <button class="btn btn-sm btn-outline-warning" [disabled]="busy" (click)="action('hold')"><i class="fa fa-pause me-1"></i>{{ 'Hold' | abpLocalization }}</button> }
            @if (d.status === 1 || d.status === 2 || d.status === 3) { <button class="btn btn-sm btn-outline-primary" [disabled]="busy" (click)="action('reopen')"><i class="fa fa-rotate-left me-1"></i>{{ 'Reopen' | abpLocalization }}</button> }
            <button class="btn btn-sm btn-outline-secondary" [disabled]="busy" (click)="showSplitPrompt = !showSplitPrompt"><i class="fa fa-code-branch me-1"></i>{{ 'Split' | abpLocalization }}</button>
          </div>
          @if (showSplitPrompt) {
            <div class="mt-3 p-3 border rounded">
              <label class="form-label">{{ 'Subject' | abpLocalization }}</label>
              <input class="form-control mb-2" [(ngModel)]="splitSubject" [placeholder]="'Subject' | abpLocalization" />
              <button class="btn btn-sm btn-primary me-2" [disabled]="busy || !splitSubject.trim()" (click)="action('split')">{{ 'Confirm' | abpLocalization }}</button>
              <button class="btn btn-sm btn-outline-secondary" (click)="showSplitPrompt = false">{{ 'Cancel' | abpLocalization }}</button>
            </div>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class IssueDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(IssueDetailService);
  private toaster = inject(ToasterService);
  d: IssueDto | null = null;
  busy = false;
  showSplitPrompt = false;
  splitSubject = '';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }

  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.busy = true;
    const onSuccess = () => {
      this.busy = false;
      this.showSplitPrompt = false;
      this.splitSubject = '';
      this.toaster.success('::SuccessfullyUpdated');
      this.ngOnInit();
    };
    const onError = (err: any) => {
      this.busy = false;
      this.toaster.error(err?.error?.error?.message ?? '::OperationFailed');
    };

    if (type === 'reply') this.service.reply(id).subscribe({ next: onSuccess, error: onError });
    else if (type === 'resolve') this.service.resolve(id).subscribe({ next: onSuccess, error: onError });
    else if (type === 'hold') this.service.hold(id).subscribe({ next: onSuccess, error: onError });
    else if (type === 'reopen') this.service.reopen(id).subscribe({ next: onSuccess, error: onError });
    else if (type === 'split') this.service.split(id, this.splitSubject.trim()).subscribe({ next: onSuccess, error: onError });
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
