import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-payment-entry-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, DocumentWorkflowComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="entry()?.paymentNumber || ('PaymentEntry' | abpLocalization)">
      @if (!entry()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <app-document-workflow [actions]="workflowActions" (actionClicked)="onAction($event)" />

        <div class="row mb-4">
          <div class="col-md-6">
            <div class="card">
              <div class="card-header"><h6 class="card-title mb-0">{{ 'PaymentDetails' | abpLocalization }}</h6></div>
              <div class="card-body">
                <table class="table table-borderless mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ 'PaymentNumber' | abpLocalization }}</td><td class="fw-bold">{{ entry()!.paymentNumber }}</td></tr>
                  <tr><td class="text-muted">{{ 'Status' | abpLocalization }}</td><td><app-status-badge [status]="entry()!.status" /></td></tr>
                  <tr><td class="text-muted">{{ 'PaymentType' | abpLocalization }}</td><td>
                    <span class="badge" [class.bg-success]="entry()!.paymentType === 'Receive'" [class.bg-primary]="entry()!.paymentType === 'Pay'" [class.bg-info]="entry()!.paymentType === 'InternalTransfer'">
                      {{ entry()!.paymentType }}
                    </span>
                  </td></tr>
                  <tr><td class="text-muted">{{ 'PostingDate' | abpLocalization }}</td><td>{{ entry()!.postingDate | date:'dd/MM/yyyy' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card">
              <div class="card-header"><h6 class="card-title mb-0">{{ 'Amount' | abpLocalization }}</h6></div>
              <div class="card-body">
                <div class="text-center">
                  <div class="text-muted small">{{ 'PaidAmount' | abpLocalization }}</div>
                  <div class="fs-2 fw-bold text-primary">
                    {{ entry()!.currencyCode }} {{ entry()!.paidAmount | number:'1.2-2' }}
                  </div>
                </div>
                <hr />
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted">{{ 'ModeOfPayment' | abpLocalization }}</td><td>{{ entry()!.modeOfPayment || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ 'ReferenceNumber' | abpLocalization }}</td><td>{{ entry()!.referenceNumber || '-' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
        </div>

        <!-- References Table (if present) -->
        @if (references().length > 0) {
          <div class="card mb-4">
            <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-link me-2"></i>{{ 'References' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'ReferenceType' | abpLocalization }}</th>
                    <th>{{ 'ReferenceNumber' | abpLocalization }}</th>
                    <th class="text-end">{{ 'TotalAmount' | abpLocalization }}</th>
                    <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
                    <th class="text-end">{{ 'AllocatedAmount' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (ref of references(); track $index) {
                    <tr>
                      <td>{{ ref.referenceType }}</td>
                      <td>{{ ref.referenceNumber || ref.referenceId }}</td>
                      <td class="text-end">{{ ref.totalAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ ref.outstandingAmount | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold">{{ ref.allocatedAmount | number:'1.2-2' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <app-activity-log documentType="PaymentEntry" [documentId]="entry()!.id" />
      }
    </abp-page>
  `
})
export class PaymentEntryDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  entry = signal<any>(null);
  references = signal<any[]>([]);

  get workflowActions(): WorkflowAction[] {
    const e = this.entry();
    if (!e) return [];
    const actions: WorkflowAction[] = [];
    if (e.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (e.status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', icon: 'check-double', color: 'success' });
    }
    if (e.status === 'Posted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.http.get<any>(`/api/app/payment-entry/${id}`).subscribe(data => {
      this.entry.set(data);
      this.references.set(data.references || []);
    });
  }

  onAction(action: string): void {
    const id = this.entry()!.id;
    switch (action) {
      case 'submit':
        this.http.post(`/api/app/payment-entry/${id}/submit`, {}).subscribe({ next: () => this.reload(), error: () => {} });
        break;
      case 'post':
        this.http.post(`/api/app/payment-entry/${id}/post`, {}).subscribe({ next: () => { this.toaster.success('Payment posted.'); this.reload(); }, error: () => {} });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.http.post(`/api/app/payment-entry/${id}/cancel`, {}).subscribe({ next: () => this.reload(), error: () => {} });
          }
        });
        break;
    }
  }

  private reload(): void {
    setTimeout(() => {
      const id = this.route.snapshot.paramMap.get('id')!;
      this.http.get<any>(`/api/app/payment-entry/${id}`).subscribe(data => {
        this.entry.set(data);
        this.references.set(data.references || []);
      });
    }, 500);
  }
}
