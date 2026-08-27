import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { SalarySlipService } from '../../proxy/human-resources/salary-slip.service';
import type { SalarySlipDto } from '../../proxy/human-resources/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-salary-slip-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SalarySlip' | abpLocalization">
      @if (slip(); as s) {
        <div class="d-flex justify-content-end gap-2 mb-3">
          @if (s.status === 0) {
            <a class="btn btn-outline-primary" [routerLink]="['/hr/salary-slips', s.id, 'edit']">
              <i class="fa fa-edit me-1"></i>{{ 'Edit' | abpLocalization }}
            </a>
            <button class="btn btn-outline-danger" (click)="deleteSlip()">
              <i class="fa fa-trash me-1"></i>{{ 'Delete' | abpLocalization }}
            </button>
            <button class="btn btn-primary" (click)="submit()">
              <i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}
            </button>
          }
          @if (s.status === 1) {
            <button class="btn btn-outline-danger" (click)="cancelSlip()">
              <i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}
            </button>
          }
        </div>
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'Employee' | abpLocalization }}</small>
                <span class="fw-bold">{{ s.employeeName ?? '—' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'Period' | abpLocalization }}</small>
                <span>{{ s.startDate | date:'dd/MM/yyyy' }} – {{ s.endDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'PostingDate' | abpLocalization }}</small>
                <span>{{ s.postingDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
                <span class="badge" [ngClass]="s.status === 1 ? 'bg-success' : 'bg-secondary'">
                  {{ s.status === 1 ? 'Submitted' : 'Draft' }}
                </span>
              </div>
            </div>
          </div>
        </div>

        @if ((s.earnings?.length ?? 0) > 0 || (s.deductions?.length ?? 0) > 0) {
          <div class="row">
            <div class="col-md-6">
              <div class="card">
                <div class="card-header fw-bold text-success"><i class="fa fa-plus-circle me-1"></i>{{ 'Earnings' | abpLocalization }}</div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      @for (c of s.earnings ?? []; track c.id) {
                        <tr><td class="ps-3">{{ c.componentName }}</td><td class="text-end pe-3 font-monospace">{{ c.amount | number:'1.2-2' }}</td></tr>
                      }
                    </tbody>
                    <tfoot><tr class="fw-bold table-light"><td class="ps-3">{{ 'Total' | abpLocalization }}</td><td class="text-end pe-3 font-monospace">{{ s.grossAmount | number:'1.2-2' }}</td></tr></tfoot>
                  </table>
                </div>
              </div>
            </div>
            <div class="col-md-6">
              <div class="card">
                <div class="card-header fw-bold text-danger"><i class="fa fa-minus-circle me-1"></i>{{ 'Deductions' | abpLocalization }}</div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      @for (c of s.deductions ?? []; track c.id) {
                        <tr><td class="ps-3">{{ c.componentName }}</td><td class="text-end pe-3 font-monospace">{{ c.amount | number:'1.2-2' }}</td></tr>
                      }
                    </tbody>
                    <tfoot><tr class="fw-bold table-light"><td class="ps-3">{{ 'Total' | abpLocalization }}</td><td class="text-end pe-3 font-monospace">{{ s.totalDeductions | number:'1.2-2' }}</td></tr></tfoot>
                  </table>
                </div>
              </div>
            </div>
          </div>

          <div class="card mt-3" style="max-width: 300px; margin-left: auto;">
            <div class="card-body text-end">
              <span class="fs-4 fw-bold text-primary">{{ 'NetPay' | abpLocalization }}: {{ s.netAmount | number:'1.2-2' }}</span>
            </div>
          </div>
        }
      } @else {
    <app-breadcrumb />
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      }
    </abp-page>
  `,
})
export class SalarySlipDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private salarySlipService = inject(SalarySlipService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  slip = signal<SalarySlipDto | null>(null);

  ngOnInit(): void {
    this.reload();
  }

  private reload(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.salarySlipService.get(id).subscribe(s => {
      this.slip.set(s);
    });
  }

  submit(): void {
    this.salarySlipService.submit(this.slip()!.id!).subscribe({
      next: () => { this.toaster.success('::SuccessfullySubmitted'); this.reload(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::OperationFailed'),
    });
  }

  cancelSlip(): void {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.salarySlipService.cancel(this.slip()!.id!).subscribe({
        next: () => { this.toaster.success('::SuccessfullyCancelled'); this.reload(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::OperationFailed'),
      });
    });
  }

  deleteSlip(): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.salarySlipService.delete(this.slip()!.id!).subscribe({
        next: () => this.router.navigate(['/hr/salary-slips']),
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::OperationFailed'),
      });
    });
  }
}
