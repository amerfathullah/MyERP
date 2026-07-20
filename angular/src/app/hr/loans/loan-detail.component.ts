import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { LoanService } from '../../proxy/human-resources/loan.service';
import type { LoanDto } from '../../proxy/human-resources/models';
import { FormsModule } from '@angular/forms';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

const LOAN_STATUS = ['Draft', 'Sanctioned', 'Disbursed', 'PartiallyRepaid', 'FullyRepaid', 'Cancelled'] as const;
const LOAN_TYPE = ['Term Loan', 'Demand Loan'] as const;
const INTEREST_METHOD = ['Diminishing Balance', 'Flat Rate'] as const;

@Component({
  selector: 'app-loan-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent, StatusBadgeComponent, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    @if (isLoading) { <app-loading-overlay /> }
    @if (!isLoading && loan) {
      <abp-page [title]="loan.loanNumber ?? 'Loan'">
        <!-- Status + Actions -->
        <div class="d-flex justify-content-between align-items-center mb-4">
          <app-status-badge [status]="statusLabel(loan.status)" />
          <div class="btn-group btn-group-sm">
            @if (loan.status === 0) {
              <button class="btn btn-outline-success" (click)="sanction()"><i class="fa fa-check me-1"></i>Sanction</button>
              <button class="btn btn-outline-danger" (click)="cancel()"><i class="fa fa-times me-1"></i>Cancel</button>
            }
            @if (loan.status === 1) {
              <button class="btn btn-outline-primary" (click)="showDisburseModal = true"><i class="fa fa-money-bill-wave me-1"></i>Disburse</button>
              <button class="btn btn-outline-danger" (click)="cancel()"><i class="fa fa-times me-1"></i>Cancel</button>
            }
            @if (loan.status === 2 || loan.status === 3) {
              <button class="btn btn-outline-success" (click)="showRepaymentModal = true"><i class="fa fa-hand-holding-dollar me-1"></i>Record Repayment</button>
            }
          </div>
        </div>

        <!-- KPI Cards -->
        <div class="row g-3 mb-4">
          <div class="col-md-3"><div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ 'LoanAmount' | abpLocalization }}</div>
            <div class="fs-4 fw-bold">{{ loan.loanAmount | number:'1.2-2' }}</div>
          </div></div></div>
          <div class="col-md-3"><div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ 'Outstanding' | abpLocalization }}</div>
            <div class="fs-4 fw-bold" [class.text-danger]="(loan.outstandingBalance ?? 0) > 0">{{ loan.outstandingBalance | number:'1.2-2' }}</div>
          </div></div></div>
          <div class="col-md-3"><div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">{{ 'InterestRate' | abpLocalization }}</div>
            <div class="fs-4 fw-bold">{{ loan.annualInterestRate }}%</div>
          </div></div></div>
          <div class="col-md-3"><div class="card text-center"><div class="card-body py-3">
            <div class="text-muted small">EMI</div>
            <div class="fs-4 fw-bold">{{ loan.emi | number:'1.2-2' }}</div>
          </div></div></div>
        </div>

        <!-- Loan Details -->
        <div class="card mb-4"><div class="card-body">
          <h6 class="card-title">{{ 'LoanDetails' | abpLocalization }}</h6>
          <div class="row">
            <div class="col-md-4"><strong>{{ 'Employee' | abpLocalization }}:</strong> {{ loan.employeeId ?? '—' }}</div>
            <div class="col-md-4"><strong>{{ 'LoanType' | abpLocalization }}:</strong> {{ LOAN_TYPE[loan.loanType ?? 0] }}</div>
            <div class="col-md-4"><strong>{{ 'InterestMethod' | abpLocalization }}:</strong> {{ INTEREST_METHOD[loan.interestMethod ?? 0] }}</div>
          </div>
          <div class="row mt-2">
            <div class="col-md-4"><strong>{{ 'Tenure' | abpLocalization }}:</strong> {{ loan.tenureMonths }} months</div>
            <div class="col-md-4"><strong>{{ 'DisbursementDate' | abpLocalization }}:</strong> {{ loan.disbursementDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-4"><strong>{{ 'RepaymentStartDate' | abpLocalization }}:</strong> {{ loan.repaymentStartDate | date:'dd/MM/yyyy' }}</div>
          </div>
        </div></div>

        <!-- Repayment Schedule -->
        @if (loan.schedule && loan.schedule.length > 0) {
          <div class="card mb-4"><div class="card-body">
            <h6 class="card-title">{{ 'RepaymentSchedule' | abpLocalization }}</h6>
            <div class="table-responsive">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>#</th>
                  <th>{{ 'DueDate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Principal' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Interest' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Total' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                </tr></thead>
                <tbody>
                  @for (entry of loan.schedule; let i = $index; track i) {
                    <tr [class.table-success]="entry.isPaid">
                      <td>{{ i + 1 }}</td>
                      <td>{{ entry.paymentDate | date:'dd/MM/yyyy' }}</td>
                      <td class="text-end">{{ entry.principalAmount | number:'1.2-2' }}</td>
                      <td class="text-end">{{ entry.interestAmount | number:'1.2-2' }}</td>
                      <td class="text-end fw-bold">{{ entry.totalPayment | number:'1.2-2' }}</td>
                      <td class="text-end">{{ entry.outstandingBalance | number:'1.2-2' }}</td>
                      <td>
                        @if (entry.isPaid) { <span class="badge bg-success">Paid</span> }
                        @else { <span class="badge bg-warning text-dark">Pending</span> }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div></div>
        }

        <!-- Activity Log -->
        <app-activity-log documentType="Loan" [documentId]="loan.id!" />

        <!-- Disburse Modal -->
        @if (showDisburseModal) {
          <div class="modal show d-block" style="background: rgba(0,0,0,0.5)">
            <div class="modal-dialog"><div class="modal-content">
              <div class="modal-header"><h5 class="modal-title">Disburse Loan</h5>
                <button class="btn-close" (click)="showDisburseModal = false"></button>
              </div>
              <div class="modal-body">
                <div class="mb-3">
                  <label class="form-label">Disbursement Date</label>
                  <input type="date" class="form-control" [(ngModel)]="disburseDate" />
                </div>
              </div>
              <div class="modal-footer">
                <button class="btn btn-secondary btn-sm" (click)="showDisburseModal = false">Close</button>
                <button class="btn btn-primary btn-sm" (click)="disburse()">Confirm Disburse</button>
              </div>
            </div></div>
          </div>
        }

        <!-- Repayment Modal -->
        @if (showRepaymentModal) {
          <div class="modal show d-block" style="background: rgba(0,0,0,0.5)">
            <div class="modal-dialog"><div class="modal-content">
              <div class="modal-header"><h5 class="modal-title">Record Repayment</h5>
                <button class="btn-close" (click)="showRepaymentModal = false"></button>
              </div>
              <div class="modal-body">
                <div class="mb-3">
                  <label class="form-label">Payment Date</label>
                  <input type="date" class="form-control" [(ngModel)]="repaymentDate" />
                </div>
                <div class="mb-3">
                  <label class="form-label">Amount</label>
                  <input type="number" class="form-control" [(ngModel)]="repaymentAmount" step="0.01" />
                </div>
              </div>
              <div class="modal-footer">
                <button class="btn btn-secondary btn-sm" (click)="showRepaymentModal = false">Close</button>
                <button class="btn btn-success btn-sm" (click)="recordRepayment()">Record Payment</button>
              </div>
            </div></div>
          </div>
        }
      </abp-page>
    }
  `
})
export class LoanDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private loanService = inject(LoanService);
  private toaster = inject(ToasterService);

  loan: LoanDto | null = null;
  isLoading = false;
  showDisburseModal = false;
  showRepaymentModal = false;
  disburseDate = new Date().toISOString().slice(0, 10);
  repaymentDate = new Date().toISOString().slice(0, 10);
  repaymentAmount = 0;

  LOAN_TYPE = LOAN_TYPE;
  INTEREST_METHOD = INTEREST_METHOD;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.load(id);
  }

  load(id: string) {
    this.isLoading = true;
    this.loanService.get(id).subscribe({
      next: loan => { this.loan = loan; this.isLoading = false; this.repaymentAmount = loan.emi ?? 0; },
      error: () => { this.isLoading = false; }
    });
  }

  statusLabel(status: number | undefined): string { return LOAN_STATUS[status ?? 0]; }

  sanction() {
    this.loanService.sanction(this.loan!.id!).subscribe({
      next: () => { this.toaster.success('Loan sanctioned'); this.load(this.loan!.id!); },
      error: () => {}
    });
  }

  disburse() {
    this.loanService.disburse(this.loan!.id!, { disbursementDate: this.disburseDate } as any).subscribe({
      next: () => { this.showDisburseModal = false; this.toaster.success('Loan disbursed'); this.load(this.loan!.id!); },
      error: () => {}
    });
  }

  recordRepayment() {
    this.loanService.recordRepayment(this.loan!.id!, { paymentDate: this.repaymentDate, amount: this.repaymentAmount } as any).subscribe({
      next: () => { this.showRepaymentModal = false; this.toaster.success('Repayment recorded'); this.load(this.loan!.id!); },
      error: () => {}
    });
  }

  cancel() {
    if (confirm('Are you sure you want to cancel this loan?')) {
      this.loanService.cancel(this.loan!.id!).subscribe({
        next: () => { this.toaster.success('Loan cancelled'); this.load(this.loan!.id!); },
        error: () => {}
      });
    }
  }
}
