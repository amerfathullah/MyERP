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
  selector: 'app-stock-entry-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, DocumentWorkflowComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="entry()?.entryNumber || ('StockEntry' | abpLocalization)">
      @if (!entry()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <app-document-workflow [actions]="workflowActions" (actionClicked)="onAction($event)" />

        <div class="row mb-4">
          <div class="col-md-6">
            <div class="card">
              <div class="card-body">
                <table class="table table-borderless mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ 'EntryNumber' | abpLocalization }}</td><td class="fw-bold">{{ entry()!.entryNumber }}</td></tr>
                  <tr><td class="text-muted">{{ 'Status' | abpLocalization }}</td><td><app-status-badge [status]="entry()!.status" /></td></tr>
                  <tr><td class="text-muted">{{ 'EntryType' | abpLocalization }}</td><td>{{ entry()!.entryType }}</td></tr>
                  <tr><td class="text-muted">{{ 'PostingDate' | abpLocalization }}</td><td>{{ entry()!.postingDate | date:'dd/MM/yyyy' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
          <div class="col-md-6">
            <div class="card">
              <div class="card-body">
                <table class="table table-borderless mb-0">
                  <tr><td class="text-muted" style="width:40%">{{ 'ReferenceType' | abpLocalization }}</td><td>{{ entry()!.referenceType || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ 'Notes' | abpLocalization }}</td><td>{{ entry()!.notes || '-' }}</td></tr>
                  <tr><td class="text-muted">{{ 'CreatedDate' | abpLocalization }}</td><td>{{ entry()!.creationTime | date:'dd/MM/yyyy HH:mm' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
        </div>

        <!-- Items Table -->
        <div class="card mb-4">
          <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-boxes-stacked me-2"></i>{{ 'Items' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ 'Item' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Quantity' | abpLocalization }}</th>
                  <th>{{ 'Source' | abpLocalization }}</th>
                  <th>{{ 'Target' | abpLocalization }}</th>
                  <th class="text-end">{{ 'ValuationRate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (item of entry()!.items; track item.id; let i = $index) {
                  <tr>
                    <td>{{ i + 1 }}</td>
                    <td>{{ item.itemId }}</td>
                    <td class="text-end">{{ item.quantity | number:'1.0-2' }}</td>
                    <td>{{ item.sourceWarehouseId || '-' }}</td>
                    <td>{{ item.targetWarehouseId || '-' }}</td>
                    <td class="text-end">{{ item.valuationRate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ (item.quantity * item.valuationRate) | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot class="table-light">
                <tr>
                  <td colspan="3" class="fw-bold text-end">{{ 'Total' | abpLocalization }}</td>
                  <td colspan="2"></td>
                  <td></td>
                  <td class="text-end fw-bold">{{ totalAmount() | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <app-activity-log documentType="StockEntry" [documentId]="entry()!.id" />
      }
    </abp-page>
  `
})
export class StockEntryDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  entry = signal<any>(null);
  totalAmount = signal(0);

  get workflowActions(): WorkflowAction[] {
    const e = this.entry();
    if (!e) return [];
    const actions: WorkflowAction[] = [];
    if (e.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (e.status === 'Submitted') {
      actions.push({ name: 'post', label: 'Post', icon: 'check-double', color: 'success' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.http.get<any>(`/api/app/stock-entry/${id}`).subscribe(data => {
      this.entry.set(data);
      this.totalAmount.set((data.items || []).reduce((sum: number, i: any) => sum + (i.quantity * i.valuationRate), 0));
    });
  }

  onAction(action: string): void {
    const id = this.entry()!.id;
    switch (action) {
      case 'submit':
        this.http.post(`/api/app/stock-entry/${id}/submit`, {}).subscribe({ next: () => this.reload(), error: () => {} });
        break;
      case 'post':
        this.http.post(`/api/app/stock-entry/${id}/post`, {}).subscribe({ next: () => { this.toaster.success('Stock entry posted.'); this.reload(); }, error: () => {} });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.http.post(`/api/app/stock-entry/${id}/cancel`, {}).subscribe({ next: () => this.reload(), error: () => {} });
          }
        });
        break;
    }
  }

  private reload(): void {
    setTimeout(() => {
      const id = this.route.snapshot.paramMap.get('id')!;
      this.http.get<any>(`/api/app/stock-entry/${id}`).subscribe(data => {
        this.entry.set(data);
        this.totalAmount.set((data.items || []).reduce((sum: number, i: any) => sum + (i.quantity * i.valuationRate), 0));
      });
    }, 500);
  }
}
