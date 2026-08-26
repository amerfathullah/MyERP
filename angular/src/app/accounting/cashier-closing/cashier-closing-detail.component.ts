import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { CashierClosingService } from '../../proxy/accounting/cashier-closing.service';
import type { CashierClosingDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-cashier-closing-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="card-title mb-0">
            {{ isNew ? 'New Cashier Closing' : ('Cashier Closing: ' + (closing?.closingNumber || '')) }}
          </h5>
          @if (!isNew && closing) {
            <small class="text-muted">
              By {{ closing.userName }} on {{ closing.date | date:'yyyy-MM-dd' }}
              <span class="badge ms-2" [ngClass]="closing.isSubmitted ? 'bg-success' : 'bg-warning text-dark'">
                {{ closing.isSubmitted ? 'Submitted' : 'Draft' }}
              </span>
            </small>
          }
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/accounting/cashier-closings" class="btn btn-outline-secondary btn-sm">Back</a>
          @if (!isNew && closing && !closing.isSubmitted) {
            <button type="button" class="btn btn-success btn-sm" (click)="submit()">
              <i class="fa fa-check me-1"></i>Submit
            </button>
          }
          @if (isNew || (closing && !closing.isSubmitted)) {
            <button type="button" class="btn btn-primary btn-sm" (click)="save()">Save</button>
          }
        </div>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-4 mb-3">
              <label class="form-label">Shift Date</label>
              <input type="date" class="form-control" formControlName="date" [readonly]="isSubmitted">
            </div>
            <div class="col-md-4 mb-3">
              <label class="form-label">From Time</label>
              <input type="text" class="form-control" formControlName="fromTime" placeholder="08:00:00" [readonly]="isSubmitted">
            </div>
            <div class="col-md-4 mb-3">
              <label class="form-label">To Time</label>
              <input type="text" class="form-control" formControlName="toTime" placeholder="17:00:00" [readonly]="isSubmitted">
            </div>

            <div class="col-12 mb-3" *ngIf="!isSubmitted">
              <button type="button" class="btn btn-outline-info btn-sm" (click)="fetchShiftTotals()">
                <i class="fa fa-calculator me-1"></i>Auto-Calculate Shift Totals
              </button>
            </div>

            <div class="col-md-3 mb-3">
              <label class="form-label">Custody (Opening Float)</label>
              <input type="number" step="0.01" class="form-control" formControlName="custody" [readonly]="isSubmitted" (input)="recalculateLocalNet()">
            </div>
            <div class="col-md-3 mb-3">
              <label class="form-label">Expense</label>
              <input type="number" step="0.01" class="form-control" formControlName="expense" [readonly]="isSubmitted" (input)="recalculateLocalNet()">
            </div>
            <div class="col-md-3 mb-3">
              <label class="form-label">Returns</label>
              <input type="number" step="0.01" class="form-control" formControlName="returns" [readonly]="isSubmitted" (input)="recalculateLocalNet()">
            </div>
            <div class="col-md-3 mb-3">
              <label class="form-label">Outstanding Amount</label>
              <input type="text" class="form-control" [value]="outstandingDisplay" readonly>
            </div>
          </div>

          <!-- Payments Breakdown -->
          <div class="mt-4">
            <div class="d-flex justify-content-between align-items-center mb-2">
              <h6 class="fw-bold mb-0">Payments Collected</h6>
              @if (!isSubmitted) {
                <button type="button" class="btn btn-outline-primary btn-sm" (click)="addPayment()">
                  <i class="fa fa-plus me-1"></i>Add Payment
                </button>
              }
            </div>
            <table class="table table-bordered table-sm align-middle">
              <thead class="table-light">
                <tr>
                  <th style="width:60%">Mode of Payment</th>
                  <th style="width:30%" class="text-end">Amount</th>
                  <th style="width:10%" class="text-center" *ngIf="!isSubmitted">Action</th>
                </tr>
              </thead>
              <tbody formArrayName="payments">
                @for (p of payments.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td>
                      <input type="text" class="form-control form-control-sm" formControlName="modeOfPayment" [readonly]="isSubmitted">
                    </td>
                    <td>
                      <input type="number" step="0.01" class="form-control form-control-sm text-end" formControlName="amount" [readonly]="isSubmitted" (input)="recalculateLocalNet()">
                    </td>
                    <td class="text-center" *ngIf="!isSubmitted">
                      <button type="button" class="btn btn-danger btn-sm p-1" (click)="removePayment(i)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td [attr.colspan]="isSubmitted ? 2 : 3" class="text-center py-2 text-muted">No payments added.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Net Amount Card -->
          <div class="card bg-light mt-3">
            <div class="card-body d-flex justify-content-between align-items-center">
              <span class="fs-5 fw-bold">Net Shift Reconciliation Amount:</span>
              <span class="fs-4 fw-bold text-primary">{{ netAmountDisplay | number:'1.2-2' }}</span>
            </div>
          </div>
        </form>
      </div>
    </div>
  `
})
export class CashierClosingDetailComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(CashierClosingService);
  private toaster = inject(ToasterService);

  isNew = true;
  closingId: string | null = null;
  closing: CashierClosingDto | null = null;
  form: FormGroup;
  outstandingDisplay = '0.00';
  netAmountDisplay = 0;

  constructor() {
    const today = new Date().toISOString().split('T')[0];
    this.form = this.fb.group({
      date: [today, Validators.required],
      fromTime: ['08:00:00', Validators.required],
      toTime: ['17:00:00', Validators.required],
      custody: [0],
      expense: [0],
      returns: [0],
      payments: this.fb.array([]),
    });
  }

  get payments(): FormArray {
    return this.form.get('payments') as FormArray;
  }

  get isSubmitted(): boolean {
    return this.closing?.isSubmitted ?? false;
  }

  ngOnInit() {
    this.closingId = this.route.snapshot.params['id'];
    this.isNew = !this.closingId || this.closingId === 'new';

    if (!this.isNew && this.closingId) {
      this.service.get(this.closingId).subscribe(res => {
        this.closing = res;
        this.outstandingDisplay = res.outstandingAmount.toFixed(2);
        this.netAmountDisplay = res.netAmount;

        this.form.patchValue({
          date: res.date.split('T')[0],
          fromTime: res.fromTime,
          toTime: res.toTime,
          custody: res.custody,
          expense: res.expense,
          returns: res.returns,
        });

        this.payments.clear();
        if (res.payments) {
          res.payments.forEach(p => {
            this.payments.push(this.fb.group({
              modeOfPayment: [p.modeOfPayment, Validators.required],
              amount: [p.amount, Validators.required]
            }));
          });
        }
      });
    }
  }

  addPayment() {
    this.payments.push(this.fb.group({
      modeOfPayment: ['Cash', Validators.required],
      amount: [0, Validators.required]
    }));
    this.recalculateLocalNet();
  }

  removePayment(i: number) {
    this.payments.removeAt(i);
    this.recalculateLocalNet();
  }

  recalculateLocalNet() {
    let totalPayments = 0;
    for (let i = 0; i < this.payments.length; i++) {
      totalPayments += Number(this.payments.at(i).get('amount')?.value || 0);
    }
    const custody = Number(this.form.get('custody')?.value || 0);
    const expense = Number(this.form.get('expense')?.value || 0);
    const returns = Number(this.form.get('returns')?.value || 0);
    const outstanding = Number(this.outstandingDisplay || 0);

    this.netAmountDisplay = totalPayments + outstanding + expense - custody + returns;
  }

  fetchShiftTotals() {
    this.service.calculateShiftTotals({
      date: this.form.get('date')?.value,
      fromTime: this.form.get('fromTime')?.value,
      toTime: this.form.get('toTime')?.value,
    }).subscribe(res => {
      this.outstandingDisplay = res.outstandingAmount.toFixed(2);
      if (res.suggestedPayments && res.suggestedPayments.length > 0) {
        this.payments.clear();
        res.suggestedPayments.forEach(p => {
          this.payments.push(this.fb.group({
            modeOfPayment: [p.modeOfPayment, Validators.required],
            amount: [p.amount, Validators.required]
          }));
        });
      }
      this.recalculateLocalNet();
      this.toaster.info('Shift totals updated.');
    });
  }

  save() {
    if (this.isNew) {
      this.service.create(this.form.value).subscribe({
        next: (res) => {
          this.toaster.success('::SuccessfullySaved');
          this.router.navigate(['/accounting/cashier-closings', res.id]);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    } else if (this.closingId) {
      this.service.update(this.closingId, this.form.value).subscribe({
        next: (res) => {
          this.closing = res;
          this.toaster.success('::SuccessfullySaved');
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    }
  }

  submit() {
    if (!this.closingId) return;
    this.service.submit(this.closingId).subscribe({
      next: (res) => {
        this.closing = res;
        this.toaster.success('Cashier Closing submitted successfully.');
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
