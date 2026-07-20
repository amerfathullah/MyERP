import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { TimesheetService } from '../../proxy/projects/timesheet.service';

@Component({
  selector: 'app-timesheet-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, DocumentWorkflowComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="ts()?.timesheetNumber || ('Timesheet' | abpLocalization)">
      @if (!ts()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <app-document-workflow [actions]="workflowActions" (actionClicked)="onAction($event)" />

        <div class="row mb-4">
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalHours' | abpLocalization }}</div>
                <div class="fs-2 fw-bold text-primary">{{ ts()!.totalHours | number:'1.1-1' }}h</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'BillableHours' | abpLocalization }}</div>
                <div class="fs-2 fw-bold text-success">{{ ts()!.totalBillableHours | number:'1.1-1' }}h</div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card">
              <div class="card-body text-center">
                <div class="text-muted small">{{ 'TotalBillingAmount' | abpLocalization }}</div>
                <div class="fs-2 fw-bold">{{ ts()!.totalBillingAmount | number:'1.2-2' }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Info -->
        <div class="card mb-4">
          <div class="card-body">
            <div class="row">
              <div class="col-md-6">
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted">{{ 'Status' | abpLocalization }}</td><td><app-status-badge [status]="ts()!.status" /></td></tr>
                  <tr><td class="text-muted">{{ 'Employee' | abpLocalization }}</td><td>{{ ts()!.employeeId }}</td></tr>
                </table>
              </div>
              <div class="col-md-6">
                <table class="table table-borderless table-sm mb-0">
                  <tr><td class="text-muted">{{ 'StartDate' | abpLocalization }}</td><td>{{ ts()!.startDate | date:'dd/MM/yyyy' }}</td></tr>
                  <tr><td class="text-muted">{{ 'EndDate' | abpLocalization }}</td><td>{{ ts()!.endDate | date:'dd/MM/yyyy' }}</td></tr>
                </table>
              </div>
            </div>
          </div>
        </div>

        <!-- Time Entries -->
        <div class="card">
          <div class="card-header"><h6 class="card-title mb-0"><i class="fa fa-clock me-2"></i>{{ 'TimeLogs' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>#</th>
                  <th>{{ 'ActivityType' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Hours' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Billable' | abpLocalization }}</th>
                  <th class="text-end">{{ 'BillingRate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (detail of ts()!.details; track $index; let i = $index) {
                  <tr>
                    <td>{{ i + 1 }}</td>
                    <td>{{ detail.activityType || '-' }}</td>
                    <td class="text-end">{{ detail.hours | number:'1.1-1' }}</td>
                    <td class="text-center">
                      @if (detail.isBillable) { <i class="fa fa-check text-success"></i> }
                      @else { <i class="fa fa-minus text-muted"></i> }
                    </td>
                    <td class="text-end">{{ detail.billingRate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ detail.billingAmount | number:'1.2-2' }}</td>
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
export class TimesheetDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private timesheetService = inject(TimesheetService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  ts = signal<any>(null);

  get workflowActions(): WorkflowAction[] {
    const t = this.ts();
    if (!t) return [];
    const actions: WorkflowAction[] = [];
    if (t.status === 'Draft') {
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (t.status === 'Submitted') {
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    return actions;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.timesheetService.get(id).subscribe(data => this.ts.set(data));
  }

  onAction(action: string): void {
    const id = this.ts()!.id;
    switch (action) {
      case 'submit':
        this.timesheetService.submit(id).subscribe({ next: () => this.reload(), error: () => {} });
        break;
      case 'cancel':
        this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(s => {
          if (s === Confirmation.Status.confirm) {
            this.timesheetService.cancel(id).subscribe({ next: () => this.reload(), error: () => {} });
          }
        });
        break;
    }
  }

  private reload(): void {
    setTimeout(() => {
      const id = this.route.snapshot.paramMap.get('id')!;
      this.timesheetService.get(id).subscribe(data => this.ts.set(data));
    }, 500);
  }
}
