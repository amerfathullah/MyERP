import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { LeaveService } from '../../proxy/human-resources/leave.service';

@Component({
  selector: 'app-leave-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="'LeaveApplication' | abpLocalization">
      @if (isLoading()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (leave(); as l) {
        <!-- KPI Cards -->
        <div class="row mb-3">
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
              <span class="badge mt-1" [class]="getStatusClass(l.status)">{{ getStatusName(l.status) }}</span>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'TotalLeaveDays' | abpLocalization }}</small>
              <h4 class="mb-0">{{ l.totalLeaveDays }}</h4>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'From' | abpLocalization }}</small>
              <strong>{{ l.fromDate | date:'dd/MM/yyyy' }}</strong>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center"><div class="card-body py-2">
              <small class="text-muted d-block">{{ 'To' | abpLocalization }}</small>
              <strong>{{ l.toDate | date:'dd/MM/yyyy' }}</strong>
            </div></div>
          </div>
        </div>

        <!-- Details -->
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'LeaveDetails' | abpLocalization }}</h6>
          <div class="row">
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'Employee' | abpLocalization }}</td><td><strong>{{ l.employeeName || '—' }}</strong></td></tr>
                <tr><td class="text-muted">{{ 'LeaveType' | abpLocalization }}</td><td>{{ l.leaveTypeName || '—' }}</td></tr>
                <tr><td class="text-muted">{{ 'HalfDay' | abpLocalization }}</td><td>{{ l.halfDay ? 'Yes' : 'No' }}</td></tr>
              </table>
            </div>
            <div class="col-md-6">
              <table class="table table-borderless table-sm mb-0">
                <tr><td class="text-muted" style="width:40%">{{ 'AppliedOn' | abpLocalization }}</td><td>{{ l.creationTime | date:'dd/MM/yyyy HH:mm' }}</td></tr>
              </table>
            </div>
          </div>
        </div></div>

        <!-- Reason -->
        @if (l.reason) {
          <div class="card mb-3"><div class="card-body">
            <h6>{{ 'Reason' | abpLocalization }}</h6>
            <p class="mb-0">{{ l.reason }}</p>
          </div></div>
        }

        <!-- Workflow Actions -->
        <div class="d-flex gap-2 mb-3">
          @if (l.status === 0) {
            <button class="btn btn-success btn-sm" (click)="approve()">
              <i class="fa fa-check me-1"></i>{{ 'Approve' | abpLocalization }}
            </button>
            <button class="btn btn-danger btn-sm" (click)="reject()">
              <i class="fa fa-times me-1"></i>{{ 'Reject' | abpLocalization }}
            </button>
          }
          @if (l.status === 0 || l.status === 1) {
            <button class="btn btn-outline-danger btn-sm" (click)="cancelLeave()">
              <i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}
            </button>
          }
        </div>

        <!-- Activity Log -->
        <app-activity-log documentType="LeaveApplication" [documentId]="leaveId" />
      }
    </abp-page>
  `
})
export class LeaveDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private leaveService = inject(LeaveService);
  private toaster = inject(ToasterService);

  leave = signal<any>(null);
  isLoading = signal(true);
  leaveId = '';

  private statusNames = ['Open', 'Approved', 'Rejected', 'Cancelled'];
  private statusClasses = ['bg-primary', 'bg-success', 'bg-danger', 'bg-secondary'];

  ngOnInit() {
    this.leaveId = this.route.snapshot.paramMap.get('id') ?? '';
    this.loadLeave();
  }

  loadLeave() {
    this.isLoading.set(true);
    this.leaveService.get(this.leaveId).subscribe({
      next: l => { this.leave.set(l); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  getStatusName(s: number) { return this.statusNames[s] ?? 'Unknown'; }
  getStatusClass(s: number) { return this.statusClasses[s] ?? 'bg-secondary'; }

  approve() {
    this.leaveService.approve(this.leaveId).subscribe({
      next: () => { this.toaster.success('::SuccessfullyApproved'); this.loadLeave(); },
      error: () => {}
    });
  }

  reject() {
    this.confirmation.warn('::RejectConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.leaveService.reject(this.leaveId).subscribe({
        next: () => { this.toaster.success('::SuccessfullyRejected'); this.loadLeave(); },
        error: () => {}
      });
    });
  }

  cancelLeave() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.leaveService.cancel(this.leaveId).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.loadLeave(); },
        error: () => {}
      });
    });
  }
}
