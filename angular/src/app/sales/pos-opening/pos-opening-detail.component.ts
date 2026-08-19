import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { PosOpeningService } from '../../proxy/sales/pos-opening.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-pos-opening-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid py-3">
      @if (!entry()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="d-flex justify-content-between align-items-center mb-3">
          <h4 class="mb-0">
            <i class="fa fa-door-open me-2 text-primary"></i>
            {{ '::PosOpeningEntry' | abpLocalization }}
          </h4>
          <div class="btn-group">
            @if (entry()!.status === 'Open') {
              <button class="btn btn-outline-warning btn-sm" (click)="cancelEntry()">
                <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
              </button>
            }
            <button class="btn btn-outline-secondary btn-sm" (click)="goBack()">
              <i class="fa fa-arrow-left me-1"></i>{{ '::Back' | abpLocalization }}
            </button>
          </div>
        </div>

        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::Status' | abpLocalization }}</div>
              <div class="mt-1"><app-status-badge [status]="entry()!.status || 'Draft'" /></div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::OpeningDate' | abpLocalization }}</div>
              <div class="fw-bold mt-1">{{ entry()!.openingDate | date:'dd/MM/yyyy HH:mm' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::TotalOpeningAmount' | abpLocalization }}</div>
              <div class="fw-bold mt-1 text-primary fs-5">{{ entry()!.totalOpeningAmount | number:'1.2-2' }}</div>
            </div>
          </div>
          <div class="col-md-3">
            <div class="card border-0 shadow-sm text-center py-3">
              <div class="text-muted small">{{ '::PaymentModes' | abpLocalization }}</div>
              <div class="fw-bold mt-1">{{ entry()!.payments?.length || 0 }}</div>
            </div>
          </div>
        </div>

        <div class="card shadow-sm mb-3">
          <div class="card-header"><h6 class="mb-0">{{ '::OpeningBalances' | abpLocalization }}</h6></div>
          <div class="card-body p-0">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ '::PaymentMode' | abpLocalization }}</th>
                  <th class="text-end">{{ '::OpeningAmount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (p of entry()!.payments || []; track p.modeName) {
                  <tr>
                    <td><i class="fa fa-credit-card me-2 text-muted"></i>{{ p.modeName }}</td>
                    <td class="text-end fw-bold">{{ p.openingAmount | number:'1.2-2' }}</td>
                  </tr>
                } @empty {
                  <tr><td colspan="2" class="text-center text-muted py-3">{{ '::NoPaymentModes' | abpLocalization }}</td></tr>
                }
              </tbody>
              <tfoot>
                <tr class="table-light fw-bold">
                  <td>{{ '::Total' | abpLocalization }}</td>
                  <td class="text-end">{{ entry()!.totalOpeningAmount | number:'1.2-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        @if (entry()!.posClosingEntryId) {
          <div class="alert alert-info">
            <i class="fa fa-info-circle me-2"></i>
            {{ '::ShiftClosedWith' | abpLocalization }}:
            <a [routerLink]="['/sales/pos-closing', entry()!.posClosingEntryId]" class="alert-link">
              {{ '::ViewClosingEntry' | abpLocalization }}
            </a>
          </div>
        }

        <app-activity-log documentType="PosOpeningEntry" [documentId]="entryId" />
      }
    </div>
  `,
})
export class PosOpeningDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);
  private service = inject(PosOpeningService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  entry = signal<any>(null);
  entryId = '';

  ngOnInit() {
    this.entryId = this.route.snapshot.params['id'];
    this.loadEntry();
  }

  loadEntry() {
    this.service.get(this.entryId).subscribe({
      next: (data) => this.entry.set(data),
      error: () => this.toaster.error(this.l('::FailedToLoad')),
    });
  }

  cancelEntry() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.cancel(this.entryId).subscribe({
        next: () => {
          this.toaster.success(this.l('::SuccessfullyCancelled'));
          this.loadEntry();
        },
        error: () => {},
      });
    });
  }

  goBack() {
    this.router.navigate(['/sales/pos-opening']);
  }

  private l(key: string): string { return this.localization.instant(key); }
}
