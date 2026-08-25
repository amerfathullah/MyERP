import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AppointmentService } from '../../proxy/crm/appointment.service';
import type { AppointmentDto } from '../../proxy/crm/models';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

const STATUS_LABELS = ['Unverified', 'Open', 'Closed'];

@Component({
  selector: 'app-appointment-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::Appointments' | abpLocalization">
      <app-breadcrumb />
      @if (d) {
        <div class="card"><div class="card-body">
          <div class="d-flex justify-content-between align-items-start">
            <h5>{{ d.customerName }}</h5>
            <span class="badge bg-info">{{ statusLabel(d.status) }}</span>
          </div>
          <div class="row mt-3">
            <div class="col-md-3"><strong>{{ '::ScheduledTime' | abpLocalization }}:</strong> {{ d.scheduledTime | date:'dd/MM/yyyy HH:mm' }}</div>
            <div class="col-md-3"><strong>{{ 'Phone' | abpLocalization }}:</strong> {{ d.phone ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'Email' | abpLocalization }}:</strong> {{ d.email ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ '::AssignedAgent' | abpLocalization }}:</strong> {{ d.assignedAgentUserId ?? '—' }}</div>
          </div>
          @if (d.details) { <div class="mt-3"><strong>{{ '::Details' | abpLocalization }}:</strong><p class="mt-1">{{ d.details }}</p></div> }

          @if (d.status === 0) {
            <div class="alert alert-info mt-3">
              <i class="fa fa-info-circle me-2"></i>{{ '::PortalAppointmentAwaitingVerification' | abpLocalization }}
              <div class="input-group input-group-sm mt-2" style="max-width: 400px;">
                <input type="text" class="form-control" [(ngModel)]="verificationToken"
                  [placeholder]="'::VerificationToken' | abpLocalization" />
                <button class="btn btn-primary" [disabled]="!verificationToken" (click)="verify()">
                  {{ '::Verify' | abpLocalization }}
                </button>
              </div>
            </div>
          }

          <div class="mt-3 d-flex gap-2">
            @if (d.status === 1) {
              <button class="btn btn-sm btn-outline-secondary" (click)="close()"><i class="fa fa-lock me-1"></i>{{ '::Close' | abpLocalization }}</button>
            }
          </div>
        </div></div>
      }
    </abp-page>
  `,
})
export class AppointmentDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(AppointmentService);
  private toaster = inject(ToasterService);

  d: AppointmentDto | null = null;
  verificationToken = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }

  statusLabel(status: number | undefined): string { return STATUS_LABELS[status ?? 0] ?? 'Open'; }

  close(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.close(id).subscribe({
      next: () => { this.toaster.success('::SuccessfullyClosed'); this.load(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  verify(): void {
    if (!this.verificationToken) return;
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.verify(id, { token: this.verificationToken }).subscribe({
      next: () => { this.toaster.success('::SuccessfullyVerified'); this.verificationToken = ''; this.load(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
