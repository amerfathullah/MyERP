import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { BankAccountBalanceService } from '../../proxy/accounting/bank-account-balance.service';
import { BankAccountService } from '../../proxy/accounting/bank-account.service';
import { BankAccountDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-bank-account-balance-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="card-title mb-0">{{ isEdit ? 'Edit' : 'New' }} Balance Snapshot</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">Bank Account *</label>
              <select class="form-select" formControlName="bankAccountId">
                <option [ngValue]="null" disabled>Select a bank account</option>
                @for (acc of bankAccounts; track acc.id) {
                  <option [ngValue]="acc.id">{{ acc.accountName }}</option>
                }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">Date *</label>
              <input type="date" class="form-control" formControlName="date">
            </div>
            <div class="col-md-3">
              <label class="form-label">Balance *</label>
              <input type="number" step="0.01" class="form-control" formControlName="balance">
            </div>
          </div>

          <div class="d-flex gap-2">
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid">Save</button>
            <a routerLink="/accounting/bank-account-balances" class="btn btn-secondary">Cancel</a>
          </div>
        </form>
      </div>
    </div>
  `
})
export class BankAccountBalanceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(BankAccountBalanceService);
  private bankAccountService = inject(BankAccountService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  form: FormGroup;
  isEdit = false;
  id: string | null = null;
  bankAccounts: BankAccountDto[] = [];

  constructor() {
    this.form = this.fb.group({
      bankAccountId: [null, Validators.required],
      date: ['', Validators.required],
      balance: [0, Validators.required],
    });
  }

  ngOnInit() {
    this.bankAccountService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe(res => {
      this.bankAccounts = res.items ?? [];
    });

    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.isEdit = true;
      this.service.get(this.id).subscribe(res => {
        this.form.patchValue({
          bankAccountId: res.bankAccountId,
          date: res.date ? res.date.substring(0, 10) : '',
          balance: res.balance,
        });
      });
    }
  }

  save() {
    if (this.form.invalid) return;
    const req = this.isEdit
      ? this.service.update(this.id!, this.form.value)
      : this.service.create(this.form.value);

    req.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/accounting/bank-account-balances']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
