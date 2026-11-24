import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ProcessDeferredAccountingService } from '../../proxy/accounting/process-deferred-accounting.service';
import { CompanyService } from '../../proxy/core/company.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { DeferredAccountingType } from '../../proxy/accounting/deferred-accounting-type.enum';
import type { DeferredAccountingPreviewDto, ProcessDeferredAccountingDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-process-deferred-accounting-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <div>
          <h5 class="card-title mb-0">
            @if (isNew) {
              {{ '::NewProcessDeferredAccounting' | abpLocalization }}
            } @else {
              {{ '::ProcessDeferredPrefix' | abpLocalization }} {{ process?.processNumber || '' }}
            }
          </h5>
          @if (!isNew && process) {
            <small class="text-muted">
              {{ '::Company' | abpLocalization }}: {{ process.companyName }}
              <span class="badge ms-2" [ngClass]="process.isCancelled ? 'bg-danger' : (process.isSubmitted ? 'bg-success' : 'bg-warning text-dark')">
                {{ (process.isCancelled ? '::Cancelled' : (process.isSubmitted ? '::Submitted' : '::Draft')) | abpLocalization }}
              </span>
            </small>
          }
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/accounting/process-deferred-accounting" class="btn btn-outline-secondary btn-sm">{{ '::Back' | abpLocalization }}</a>
          @if (!isNew && process && process.isSubmitted && !process.isCancelled) {
            <button type="button" class="btn btn-danger btn-sm" (click)="cancel()">
              <i class="fa fa-ban me-1"></i>{{ '::Cancel' | abpLocalization }}
            </button>
          }
          @if (!isNew && process && !process.isSubmitted) {
            <button type="button" class="btn btn-success btn-sm" (click)="submit()">
              <i class="fa fa-play me-1"></i>{{ '::ProcessAndSubmit' | abpLocalization }}
            </button>
          }
          @if (isNew || (process && !process.isSubmitted)) {
            <button type="button" class="btn btn-outline-info btn-sm" (click)="preview()" [disabled]="previewLoading || form.invalid">
              @if (previewLoading) { <i class="fa fa-spinner fa-spin me-1"></i> } @else { <i class="fa fa-eye me-1"></i> }
              {{ '::PreviewRecognition' | abpLocalization }}
            </button>
            <button type="button" class="btn btn-primary btn-sm" (click)="save()">{{ '::Save' | abpLocalization }}</button>
          }
        </div>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row">
            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::Company' | abpLocalization }}</label>
              <select class="form-select" formControlName="companyId">
                <option value="" disabled>{{ '::SelectCompany' | abpLocalization }}</option>
                @for (c of companies; track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::Type' | abpLocalization }}</label>
              <select class="form-select" formControlName="type">
                <option [ngValue]="DeferredAccountingType.Income">{{ '::DeferredIncomeOption' | abpLocalization }}</option>
                <option [ngValue]="DeferredAccountingType.Expense">{{ '::DeferredExpenseOption' | abpLocalization }}</option>
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::SpecificAccountOptionalFilter' | abpLocalization }}</label>
              <select class="form-select" formControlName="accountId">
                <option [ngValue]="null">{{ '::AllDeferredAccounts' | abpLocalization }}</option>
                @for (a of accounts; track a.id) {
                  <option [value]="a.id">{{ a.accountName }} ({{ a.accountCode }})</option>
                }
              </select>
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::PostingDateForRecognitionJe' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="postingDate">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::ServiceStartDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="startDate">
            </div>

            <div class="col-md-6 mb-3">
              <label class="form-label">{{ '::ServiceEndDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="endDate">
            </div>
          </div>

          @if (previewResult) {
            <div class="card mt-3 border-info">
              <div class="card-header bg-info bg-opacity-10 d-flex justify-content-between align-items-center">
                <strong>{{ '::RecognitionPreview' | abpLocalization }}</strong>
                <span class="small font-monospace">
                  {{ previewResult.totalInvoicesCount ?? 0 }} {{ '::Items' | abpLocalization }} — {{ '::Total' | abpLocalization }}: {{ (previewResult.totalAmountToRecognize ?? 0) | number:'1.2-2' }}
                </span>
              </div>
              <div class="card-body p-0">
                <div class="table-responsive">
                  <table class="table table-sm table-hover mb-0">
                    <thead class="table-light">
                      <tr>
                        <th>{{ '::InvoiceNumber' | abpLocalization }}</th>
                        <th>{{ '::Item' | abpLocalization }}</th>
                        <th>{{ '::ServicePeriod' | abpLocalization }}</th>
                        <th class="text-end">{{ '::Total' | abpLocalization }}</th>
                        <th class="text-end">{{ '::ToRecognize' | abpLocalization }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (item of previewResult.items ?? []; track $index) {
                        <tr>
                          <td class="font-monospace small">{{ item.invoiceNumber }}</td>
                          <td>{{ item.itemDescription }}</td>
                          <td class="small">{{ item.serviceStartDate | date:'yyyy-MM-dd' }} ~ {{ item.serviceEndDate | date:'yyyy-MM-dd' }}</td>
                          <td class="text-end font-monospace">{{ (item.totalAmount ?? 0) | number:'1.2-2' }}</td>
                          <td class="text-end font-monospace fw-bold text-primary">{{ (item.amountToRecognize ?? 0) | number:'1.2-2' }}</td>
                        </tr>
                      } @empty {
                        <tr>
                          <td colspan="5" class="text-center text-muted py-3">{{ '::NoDeferredItemsToRecognize' | abpLocalization }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          }

          @if (!isNew && process && process.isSubmitted) {
            <div class="alert alert-info mt-3">
              <i class="fa fa-info-circle me-1"></i>
              <strong>{{ '::RecognitionSummary' | abpLocalization }}:</strong> {{ '::ProcessDeferredBookedSummary' | abpLocalization:(process.entriesProcessed ?? 0).toString() }}
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
  private localization = inject(LocalizationService);

  DeferredAccountingType = DeferredAccountingType;
  isNew = true;
  processId: string | null = null;
  process: ProcessDeferredAccountingDto | null = null;
  previewResult: DeferredAccountingPreviewDto | null = null;
  previewLoading = false;
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

  preview() {
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    this.previewLoading = true;
    this.service.previewDeferredAccounting({
      companyId: v.companyId,
      type: v.type,
      accountId: v.accountId || null,
      startDate: v.startDate,
      endDate: v.endDate,
    }).subscribe({
      next: res => {
        this.previewResult = res;
        this.previewLoading = false;
      },
      error: (err) => {
        this.previewLoading = false;
        this.toaster.error(err?.error?.error?.message ?? '::Failed');
      },
    });
  }

  save() {
    if (this.isNew) {
      this.service.create(this.form.value).subscribe({
        next: (res) => {
          this.toaster.success('::SuccessfullySaved');
          this.router.navigate(['/accounting/process-deferred-accounting', res.id]);
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
      });
    } else if (this.processId) {
      this.service.update(this.processId, this.form.value).subscribe({
        next: (res) => {
          this.process = res;
          this.toaster.success('::SuccessfullySaved');
        },
        error: (err) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
      });
    }
  }

  submit() {
    if (!this.processId) return;
    this.service.submit(this.processId).subscribe({
      next: (res) => {
        this.process = res;
        this.form.disable();
        this.toaster.success(
          this.localization.instant('::ProcessDeferredProcessed', String(res.entriesProcessed ?? 0))
        );
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
    });
  }

  cancel() {
    if (!this.processId) return;
    this.service.cancel(this.processId).subscribe({
      next: (res) => {
        this.process = res;
        this.toaster.success('::ProcessDeferredCancelled');
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? '::Failed'),
    });
  }
}
