import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ToasterService } from '@abp/ng.theme.shared';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-maintenance-visit-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    @if (visit()) {
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">
            {{ 'MyERP::MaintenanceVisit' | abpLocalization }}
            <app-status-badge [status]="getStatusLabel(visit()!.completionStatus ?? 0)" />
          </h5>
          <div class="btn-group">
            @if (visit()!.completionStatus === 0) {
              <a [routerLink]="['..', visit()!.id, 'edit']" class="btn btn-sm btn-outline-primary">
                <i class="bi bi-pencil me-1"></i>{{ 'MyERP::Edit' | abpLocalization }}
              </a>
              <button class="btn btn-sm btn-success" (click)="complete()">
                <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::MarkComplete' | abpLocalization }}
              </button>
              <button class="btn btn-sm btn-outline-danger" (click)="cancel()">
                <i class="bi bi-x-lg me-1"></i>{{ 'MyERP::Cancel' | abpLocalization }}
              </button>
            }
            @if (visit()!.completionStatus === 1) {
              <button class="btn btn-sm btn-success" (click)="complete()">
                <i class="bi bi-check-lg me-1"></i>{{ 'MyERP::MarkComplete' | abpLocalization }}
              </button>
            }
          </div>
        </div>
        <div class="card-body">
          <div class="row mb-4">
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::VisitDate' | abpLocalization }}</label>
              <div>{{ visit()!.visitDate | date:'mediumDate' }}</div>
            </div>
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::MaintenanceType' | abpLocalization }}</label>
              <div>
                <span class="badge" [class]="getTypeBadge(visit()!.maintenanceType)">
                  {{ visit()!.maintenanceType }}
                </span>
              </div>
            </div>
            <div class="col-md-4">
              <label class="form-label text-muted small">{{ 'MyERP::Customer' | abpLocalization }}</label>
              <div>{{ customerName() || '-' }}</div>
            </div>
          </div>

          @if (visit()!.purposes?.length) {
            <h6 class="mt-4 mb-2">{{ 'MyERP::WorkPerformed' | abpLocalization }}</h6>
            <div class="table-responsive">
              <table class="table table-sm table-bordered">
                <thead class="table-light">
                  <tr>
                    <th>{{ 'MyERP::Item' | abpLocalization }}</th>
                    <th>{{ 'MyERP::WorkDone' | abpLocalization }}</th>
                    <th>{{ 'MyERP::Details' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (p of visit()!.purposes; track p.id) {
                    <tr>
                      <td>{{ p.itemName || '-' }}</td>
                      <td>{{ p.workDone }}</td>
                      <td>{{ p.workDetails || '-' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    }
  `,
})
export class MaintenanceVisitDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(MaintenanceService);
  private customerService = inject(CustomerService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  visit = signal<any>(null);
  customerName = signal('');

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.getVisit(id).subscribe({
      next: (v) => {
        this.visit.set(v);
        if (v.customerId) this.loadCustomerName(v.customerId);
      },
    });
  }

  private loadCustomerName(id: string) {
    this.customerService.get(id).subscribe({
      next: (c: any) => this.customerName.set(c.customerName || c.id),
      error: () => {},
    });
  }

  complete() {
    this.confirmation.warn('MyERP::AreYouSure', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.completeVisit(this.visit()!.id).subscribe({
          next: (v) => {
            this.visit.set(v);
            this.toaster.success('MyERP::SuccessfullyCompleted');
          },
        });
      }
    });
  }

  cancel() {
    this.confirmation.warn('MyERP::AreYouSure', 'MyERP::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.cancelVisit(this.visit()!.id).subscribe({
          next: (v) => {
            this.visit.set(v);
            this.toaster.success('MyERP::SuccessfullyCancelled');
          },
        });
      }
    });
  }

  getStatusLabel(status: number): string {
    return ['Open', 'Partially Completed', 'Completed', 'Cancelled'][status] ?? 'Unknown';
  }

  getStatusVariant(status: number): string {
    return ['warning', 'info', 'success', 'danger'][status] ?? 'secondary';
  }

  getTypeBadge(type: string): string {
    switch (type) {
      case 'Breakdown': return 'bg-danger text-white';
      case 'Unscheduled': return 'bg-warning text-dark';
      default: return 'bg-info text-white';
    }
  }
}
