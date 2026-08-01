import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ContractService } from '../../proxy/crm/contract.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  standalone: true,
  selector: 'app-contract-detail',
  imports: [CommonModule, LocalizationPipe, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    @if (loading()) {
      <div class="d-flex justify-content-center p-5"><div class="spinner-border text-primary"></div></div>
    } @else if (contract()) {
      <div class="row mb-3">
        <div class="col"><h4 class="mb-0">{{ contract().contractName || contract().name }}</h4></div>
        <div class="col-auto d-flex gap-2 align-items-center">
          <app-status-badge [status]="contract().status || 'Draft'" />
          @if (contract().status === 'Active' || contract().status === 'Unsigned') {
            <button class="btn btn-sm btn-outline-success" (click)="sign()">
              <i class="fas fa-signature me-1"></i>{{ '::Sign' | abpLocalization }}
            </button>
          }
          @if (contract().status === 'Active') {
            <button class="btn btn-sm btn-outline-danger" (click)="cancel()">
              <i class="fas fa-times me-1"></i>{{ '::Cancel' | abpLocalization }}
            </button>
          }
        </div>
      </div>

      <div class="row g-3 mb-4">
        <div class="col-md-3">
          <div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ '::StartDate' | abpLocalization }}</div>
            <div class="fw-bold">{{ contract().startDate | date:'dd/MM/yyyy' }}</div>
          </div></div>
        </div>
        <div class="col-md-3">
          <div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ '::EndDate' | abpLocalization }}</div>
            <div class="fw-bold">{{ contract().endDate ? (contract().endDate | date:'dd/MM/yyyy') : '—' }}</div>
          </div></div>
        </div>
        <div class="col-md-3">
          <div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ '::ContractValue' | abpLocalization }}</div>
            <div class="fw-bold">{{ contract().contractValue | number:'1.2-2' }}</div>
          </div></div>
        </div>
        <div class="col-md-3">
          <div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ '::PartyName' | abpLocalization }}</div>
            <div class="fw-bold">{{ contract().partyName || '—' }}</div>
          </div></div>
        </div>
      </div>

      @if (contract().contractTerms) {
        <div class="card mb-4">
          <div class="card-header"><h6 class="mb-0">{{ '::ContractTerms' | abpLocalization }}</h6></div>
          <div class="card-body"><p class="mb-0 white-space-pre-wrap">{{ contract().contractTerms }}</p></div>
        </div>
      }

      <app-activity-log documentType="Contract" [documentId]="contract().id" />
    }
  `,
})
export class ContractDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private contractService = inject(ContractService);
  private toaster = inject(ToasterService);
  private localization = inject(LocalizationService);

  contract = signal<any>(null);
  loading = signal(true);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.router.navigate(['/crm/contracts']); return; }
    this.contractService.get(id).subscribe({
      next: (c: any) => { this.contract.set(c); this.loading.set(false); },
      error: () => { this.router.navigate(['/crm/contracts']); },
    });
  }

  sign() {
    this.contractService.sign(this.contract().id).subscribe({
      next: () => { this.toaster.success(this.l('::SuccessfullyUpdated')); this.reload(); },
      error: () => {},
    });
  }

  cancel() {
    this.contractService.cancel(this.contract().id).subscribe({
      next: () => { this.toaster.success(this.l('::SuccessfullyCancelled')); this.reload(); },
      error: () => {},
    });
  }

  private reload() {
    this.contractService.get(this.contract().id).subscribe({
      next: (c: any) => this.contract.set(c),
    });
  }

  private l(key: string): string { return this.localization.instant(key); }
}
