import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ExpenseClaimService, type ExpenseClaimDto } from '../../proxy/sales/additional-proxies.service';

@Component({
  selector: 'app-expense-claim-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ExpenseClaims' | abpLocalization">
      @if (claim) {
        <div class="card mb-3"><div class="card-body">
          <div class="row mb-2">
            <div class="col-md-4"><strong>{{ 'Employee' | abpLocalization }}:</strong> {{ claim.employeeName }}</div>
            <div class="col-md-3"><strong>{{ 'Date' | abpLocalization }}:</strong> {{ claim.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-3"><strong>{{ 'Type' | abpLocalization }}:</strong> {{ claim.expenseType }}</div>
            <div class="col-md-2"><span class="badge" [ngClass]="statusClass(claim.status)">{{ statusLabel(claim.status) }}</span></div>
          </div>
        </div></div>
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'Expenses' | abpLocalization }}</h6>
          <table class="table table-sm">
            <thead><tr><th>{{ 'Date' | abpLocalization }}</th><th>{{ 'Description' | abpLocalization }}</th><th class="text-end">{{ 'Amount' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (e of claim.expenses ?? []; track e.id) {
                <tr><td>{{ e.expenseDate | date:'dd/MM/yyyy' }}</td><td>{{ e.description }}</td><td class="text-end">{{ e.amount | number:'1.2-2' }}</td></tr>
              }
            </tbody>
            <tfoot><tr><th colspan="2">{{ 'Total' | abpLocalization }}</th><th class="text-end">{{ claim.totalClaimedAmount | number:'1.2-2' }}</th></tr></tfoot>
          </table>
        </div></div>
        <div class="d-flex gap-2">
          @if (claim.status === 0) {
            <button class="btn btn-success btn-sm" (click)="approve()"><i class="fa fa-check me-1"></i>{{ 'Approve' | abpLocalization }}</button>
            <button class="btn btn-danger btn-sm" (click)="reject()"><i class="fa fa-times me-1"></i>{{ 'Reject' | abpLocalization }}</button>
          }
        </div>
      }
    </abp-page>
  `,
})
export class ExpenseClaimDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(ExpenseClaimService);
  claim: ExpenseClaimDto | null = null;
  private get id() { return this.route.snapshot.paramMap.get('id')!; }

  ngOnInit() { this.load(); }

  load() {
    this.service.get(this.id).subscribe((r) => this.claim = r);
  }

  approve() { this.service.approve(this.id).subscribe(() => this.load()); }
  reject() { this.service.reject(this.id).subscribe(() => this.load()); }

  statusLabel(s: number) { return ['Draft', 'Submitted', 'Approved', '', 'Cancelled', 'Rejected'][s] ?? 'Draft'; }
  statusClass(s: number) { return ['bg-secondary', 'bg-primary', 'bg-success', '', 'bg-danger', 'bg-warning'][s] ?? 'bg-secondary'; }
}
