import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProcessDeferredAccountingService } from '../../proxy/accounting/process-deferred-accounting.service';
import { CompanyService } from '../../proxy/core/company.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { DeferredAccountingType, type ProcessDeferredAccountingDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-process-deferred-accounting-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="card-title mb-0">
            {{ isNew ? 'New Process Deferred Accounting' : ('Process: ' + (process?.processNumber || '')) }}
          </h5>
          @if (!isNew && process) {
            <small class="text-muted">
              Company: {{ process.companyName }}
              <span class="badge ms-2" [ngClass]="process.isCancelled ? 'bg-danger' : (process.isSubmitted ? 'bg-success' : 'bg-warning text-dark')">
                {{ process.isCancelled ? 'Cancelled' : (process.isSubmitted ? 'Submitted' : 'Draft') }}
              </span>
            </small>
          }
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/accounting/process-deferred-accounting" class="btn btn-outline-secondary btn-sm">Back</a>
          @if (!isNew && process && process.isSubmitted && !process.isCancelled) {
            <button type="button" class="btn btn-danger btn-sm" (click)="cancel()">
              <i class="fa fa-ban me-1"></i>Cancel
            </button>
          }
          @if (!isNew && process && !process.isSubmitted) {
            <button type="button" class="btn btn-success btn-sm" (click)="submit()">
              <i class="fa fa-play me-1"></i>Process & Submit
            </button>
          }
          @if (isNew || (process && !process.isSubmitted)) {
            <button type="button" class="btn btn-primary btn-sm" (click)="save()">Save</button>
          }
        </div>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">Company</label>
              <select class="form-select" formControlName="companyId">
                <option value="" disabled>Select Company</option>
                @for (c of companies; track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Type</label>
              <select class="form-select" formControlName="type">
                <option [ngValue]="DeferredAccountingType.Income">Income (Deferred Revenue → Sales Revenue)</option>
                <option [ngValue]="DeferredAccountingType.Expense">Expense (Deferred Expense → Operating Expense)</option>
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Specific Account (Optional Filter)</label>
              <select class="form-select" formControlName="accountId">
                <option [ngValue]="null">All Deferred Accounts</option>
                @for (a of accounts; track a.id) {
                  <option [value]="a.id">{{ a.accountName }} ({{ a.accountNumber }})</option>
                }
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Posting Date (For Recognition Journal Entries)</label>
              <input type="date" class="form-control" formControlName="postingDate">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Service Start Date</label>
              <input type="date" class="form-control" formControlName="startDate">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">Service End Date</label>
              <input type="date" class="form-control" formControlName="endDate">
            </div>
          </div>

          @if (!isNew && process && process.isSubmitted) {
            <div class="alert alert-info mt-3">
              <i class="fa fa-info-circle me-1"></i>
              <strong>Recognition Summary:</strong> Successfully booked <strong>{{ process.entriesProcessed }}</strong> deferred accounting Journal Entries.
            </div>
          }
        </form>
      </div>
    </div>
  `
})
export class ProcessDeferredAccountingDetailComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(ProcessDeferredAccountingService);
  private companyService = inject(CompanyService);
  private accountService = inject(AccountService);
  private toaster = inject(ToasterService);

  DeferredAccountingType = DeferredAccountingType;
  isNew = true;
  processId: string | null = null;
  process: ProcessDeferredAccountingDto | null = null;
  form: FormGroup;
  companies: any[] = [];
  accounts: any[] = [];

  constructor() {
    const today = new Date().toISOString().split('T')[0];
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      type: [DeferredAccountingType.Income, Validators.required],
      accountId: [null],
      postingDate: [today, Validators.required],
      startDate: [today, Validators.required],
      endDate: [today, Validators.required],
    });
  }

  ngOnInit() {
    this.processId = this.route.snapshot.params['id'];
    this.isNew = !this.processId || this.processId === 'new';

    this.companyService.getList({ skipCount: 0, maxResultCount: 100 } as any).subscribe(res => {
      this.companies = res.items || [];
      if (this.isNew && this.companies.length > 0 && !this.form.get('companyId')?.value) {
        this.form.patchValue({ companyId: this.companies[0].id });
      }
    });

    this.accountService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe(res => {
      this.accounts = res.items || [];
    });

    if (!this.isNew && this.processId) {
      this.service.get(this.processId).subscribe(res => {
        this.process = res;
        this.form.patchValue({
          companyId: res.companyId,
          type: res.type,
          accountId: res.accountId,
          postingDate: res.postingDate.split('T')[0],
          startDate: res.startDate.split('T')[0],
          endDate: res.endDate.split('T')[0],
        });

        if (res.isSubmitted || res.isCancelled) {
          this.form.disable();
        }
      });
    }
  }

  save() {
    if (this.isNew) {
      this.service.create(this.form.value).subscribe({
        next: (res) => {
          this.toaster.success('::SuccessfullySaved');
          this.router.navigate(['/accounting/process-deferred-accounting', res.id]);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    } else if (this.processId) {
      this.service.update(this.processId, this.form.value).subscribe({
        next: (res) => {
          this.process = res;
          this.toaster.success('::SuccessfullySaved');
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
      });
    }
  }

  submit() {
    if (!this.processId) return;
    this.service.submit(this.processId).subscribe({
      next: (res) => {
        this.process = res;
        this.form.disable();
        this.toaster.success(`Successfully processed ${res.entriesProcessed} deferred accounting entries.`);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }

  cancel() {
    if (!this.processId) return;
    this.service.cancel(this.processId).subscribe({
      next: (res) => {
        this.process = res;
        this.toaster.success('Process Deferred Accounting cancelled.');
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed'),
    });
  }
}
