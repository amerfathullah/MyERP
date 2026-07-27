import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PosClosingService } from '../../proxy/sales/pos-closing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-pos-closing-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe, SaveShortcutDirective, AutoValidationDirective, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid py-3">
      <h4 class="mb-3">
        <i class="fa fa-register me-2 text-primary"></i>
        {{ '::NewPosClosingEntry' | abpLocalization }}
      </h4>

      <form [formGroup]="form" (appSaveShortcut)="save()">
        <div class="card shadow-sm mb-3">
          <div class="card-header"><h6 class="mb-0">{{ '::ClosingDetails' | abpLocalization }}</h6></div>
          <div class="card-body">
            <div class="row g-3">
              <div class="col-md-6">
                <label class="form-label">{{ '::PosOpeningEntry' | abpLocalization }} *</label>
                <select class="form-select" formControlName="posOpeningEntryId">
                  <option value="">{{ '::SelectOpenEntry' | abpLocalization }}</option>
                  @for (e of openEntries(); track e.id) {
                    <option [value]="e.id">{{ e.openingDate | date:'dd/MM/yyyy HH:mm' }} — {{ e.totalOpeningAmount | number:'1.2-2' }}</option>
                  }
                </select>
              </div>
              <div class="col-md-6">
                <label class="form-label">{{ '::PostingDate' | abpLocalization }}</label>
                <input type="datetime-local" class="form-control" formControlName="postingDate" readonly>
                <small class="text-muted">{{ '::PosClosingAlwaysNow' | abpLocalization }}</small>
              </div>
            </div>
          </div>
        </div>

        <!-- Payment Reconciliation -->
        <div class="card shadow-sm mb-3">
          <div class="card-header d-flex justify-content-between align-items-center">
            <h6 class="mb-0">{{ '::PaymentReconciliation' | abpLocalization }}</h6>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addPayment()">
              <i class="fa fa-plus me-1"></i>{{ '::AddPayment' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table mb-0">
              <thead>
                <tr>
                  <th>{{ '::PaymentMode' | abpLocalization }}</th>
                  <th>{{ '::ExpectedAmount' | abpLocalization }}</th>
                  <th>{{ '::ClosingAmount' | abpLocalization }}</th>
                  <th>{{ '::Difference' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody formArrayName="payments">
                @for (payment of paymentsArray.controls; track $index; let i = $index) {
                  <tr [formGroupName]="i">
                    <td><input type="text" class="form-control form-control-sm" formControlName="modeOfPayment" [placeholder]="'::Placeholder:PaymentMode' | abpLocalization"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="expectedAmount" (change)="updateDifference(i)"></td>
                    <td><input type="number" class="form-control form-control-sm" formControlName="closingAmount" (change)="updateDifference(i)"></td>
                    <td class="align-middle">
                      <span [class.text-success]="payment.get('difference')!.value >= 0"
                            [class.text-danger]="payment.get('difference')!.value < 0">
                        {{ payment.get('difference')!.value | number:'1.2-2' }}
                      </span>
                    </td>
                    <td>
                      <button type="button" class="btn btn-outline-danger btn-sm" (click)="removePayment(i)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr class="table-light fw-bold">
                  <td>{{ '::Total' | abpLocalization }}</td>
                  <td>{{ totalExpected() | number:'1.2-2' }}</td>
                  <td>{{ totalClosing() | number:'1.2-2' }}</td>
                  <td [class.text-success]="totalDifference() >= 0" [class.text-danger]="totalDifference() < 0">
                    {{ totalDifference() | number:'1.2-2' }}
                  </td>
                  <td></td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <button type="button" class="btn btn-outline-secondary" (click)="goBack()">{{ '::Cancel' | abpLocalization }}</button>
          <button type="button" class="btn btn-primary" (click)="save()" [disabled]="saving()">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
          </button>
        </div>
      </form>
    </div>
  `,
})
export class PosClosingFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private service = inject(PosClosingService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private http = inject(HttpClient);
  private localization = inject(LocalizationService);

  saving = signal(false);
  openEntries = signal<any[]>([]);

  form = this.fb.group({
    companyId: ['', Validators.required],
    posOpeningEntryId: ['', Validators.required],
    postingDate: [new Date().toISOString().slice(0, 16)],
    payments: this.fb.array([]),
  });

  get paymentsArray() { return this.form.get('payments') as FormArray; }

  totalExpected = signal(0);
  totalClosing = signal(0);
  totalDifference = signal(0);

  ngOnInit() {
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });

    // Load open POS entries
    this.http.get<any>('/api/app/pos-opening?maxResultCount=50').subscribe({
      next: (res) => {
        this.openEntries.set((res.items || []).filter((e: any) => e.status === 'Open'));
      },
      error: () => {},
    });

    this.addPayment(); // Start with one payment row
  }

  addPayment() {
    this.paymentsArray.push(this.fb.group({
      modeOfPayment: ['Cash'],
      expectedAmount: [0],
      closingAmount: [0],
      difference: [0],
    }));
  }

  removePayment(index: number) {
    this.paymentsArray.removeAt(index);
    this.recalculateTotals();
  }

  updateDifference(index: number) {
    const group = this.paymentsArray.at(index);
    const expected = group.get('expectedAmount')!.value || 0;
    const closing = group.get('closingAmount')!.value || 0;
    group.get('difference')!.setValue(closing - expected);
    this.recalculateTotals();
  }

  recalculateTotals() {
    let exp = 0, cls = 0;
    for (let i = 0; i < this.paymentsArray.length; i++) {
      const g = this.paymentsArray.at(i);
      exp += g.get('expectedAmount')!.value || 0;
      cls += g.get('closingAmount')!.value || 0;
    }
    this.totalExpected.set(exp);
    this.totalClosing.set(cls);
    this.totalDifference.set(cls - exp);
  }

  save() {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const dto = {
      companyId: raw.companyId,
      posOpeningEntryId: raw.posOpeningEntryId || undefined,
      payments: (raw.payments || []).filter((p: any) => p.modeOfPayment).map((p: any) => ({
        modeOfPayment: p.modeOfPayment,
        expectedAmount: p.expectedAmount || 0,
        closingAmount: p.closingAmount || 0,
      })),
      invoices: [], // POS invoices linked during the shift
    };

    this.service.create(dto as any).subscribe({
      next: (created) => {
        this.toaster.success(this.l('::SuccessfullyCreated'));
        this.router.navigate(['/sales/pos-closing', created.id]);
      },
      error: () => this.saving.set(false),
    });
  }

  goBack() {
    this.router.navigate(['/sales/pos-closing']);
  }

  hasUnsavedChanges() { return this.form.dirty; }

  private l(key: string): string { return this.localization.instant(key); }
}
