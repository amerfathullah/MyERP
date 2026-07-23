import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DunningService } from '../../proxy/sales/dunning.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

interface OverdueInvoice {
  id: string;
  invoiceNumber: string;
  dueDate: string;
  grandTotal: number;
  outstandingAmount: number;
  overdueDays: number;
  selected: boolean;
}

@Component({
  selector: 'app-dunning-form', standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, PageModule, LocalizationPipe, SaveShortcutDirective, AutoValidationDirective],
  template: `
    <abp-page [title]="'NewDunning' | abpLocalization">
      <form [formGroup]="form" (appSaveShortcut)="save()">
      <div class="card mb-3"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Customer' | abpLocalization }} *</label>
            <select class="form-select" formControlName="customerId" (change)="onCustomerChanged()">
              <option value="">{{ 'Select' | abpLocalization }}...</option>
              @for (c of customers(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" formControlName="postingDate" />
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ 'Level' | abpLocalization }}</label>
            <input type="number" class="form-control" formControlName="dunningLevel" min="1" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Fee' | abpLocalization }}</label>
            <input type="number" class="form-control" formControlName="dunningFee" step="0.01" />
          </div>
        </div>
      </div></div>

      <div class="card mb-3"><div class="card-body">
        <div class="d-flex justify-content-between align-items-center mb-2">
          <h6 class="mb-0">{{ 'OverdueInvoices' | abpLocalization }}</h6>
          @if (form.get('customerId')?.value) {
            <button type="button" class="btn btn-sm btn-outline-primary" (click)="fetchOverdueInvoices()" [disabled]="fetchingInvoices()">
              <i class="fa fa-download me-1"></i>{{ 'GetOutstanding' | abpLocalization }}
            </button>
          }
        </div>

        @if (overdueInvoices().length > 0) {
          <table class="table table-sm table-hover">
            <thead><tr>
              <th><input type="checkbox" (change)="toggleAll($event)" /></th>
              <th>{{ 'Invoice' | abpLocalization }}</th>
              <th>{{ 'DueDate' | abpLocalization }}</th>
              <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
              <th class="text-end">{{ 'DaysOverdue' | abpLocalization }}</th>
              <th class="text-end">{{ 'Interest' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (inv of overdueInvoices(); track inv.id) {
                <tr [class.table-active]="inv.selected">
                  <td><input type="checkbox" [(ngModel)]="inv.selected" [ngModelOptions]="{standalone: true}" (change)="recalculate()" /></td>
                  <td>{{ inv.invoiceNumber }}</td>
                  <td>{{ inv.dueDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end text-danger fw-bold">{{ inv.outstandingAmount | number:'1.2-2' }}</td>
                  <td class="text-end">{{ inv.overdueDays }}</td>
                  <td class="text-end">{{ calculateInterest(inv) | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
            <tfoot><tr class="fw-bold">
              <td colspan="3">{{ 'Total' | abpLocalization }}</td>
              <td class="text-end text-danger">{{ totalOutstanding() | number:'1.2-2' }}</td>
              <td></td>
              <td class="text-end">{{ totalInterest() | number:'1.2-2' }}</td>
            </tr></tfoot>
          </table>
        } @else if (form.get('customerId')?.value) {
          <p class="text-muted">{{ 'NoOutstandingInvoices' | abpLocalization }}</p>
        } @else {
          <p class="text-muted">{{ 'SelectCustomerToViewOutstanding' | abpLocalization }}</p>
        }
      </div></div>

      <div class="card"><div class="card-body">
        <div class="row align-items-center">
          <div class="col-md-4">
            <span class="fs-5 fw-bold text-danger">{{ 'GrandTotal' | abpLocalization }}: {{ grandTotal() | number:'1.2-2' }}</span>
            <small class="d-block text-muted">{{ 'Outstanding' | abpLocalization }} + {{ 'Interest' | abpLocalization }} + {{ 'Fee' | abpLocalization }}</small>
          </div>
          <div class="col-md-8 text-end">
            <a class="btn btn-secondary me-2" routerLink="/sales/dunnings">{{ 'Cancel' | abpLocalization }}</a>
            <button type="submit" class="btn btn-primary" (click)="save()" [disabled]="saving || !hasSelectedInvoices()">
              <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
            </button>
          </div>
        </div>
      </div></div>
      </form>
    </abp-page>
  `,
})
export class DunningFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(DunningService);
  private customerService = inject(CustomerService);
  private http = inject(HttpClient);
  private router = inject(Router);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = false;
  customers = signal<{ id: string; name: string }[]>([]);
  overdueInvoices = signal<OverdueInvoice[]>([]);
  fetchingInvoices = signal(false);
  totalOutstanding = signal(0);
  totalInterest = signal(0);

  form = this.fb.group({
    customerId: ['', Validators.required],
    postingDate: [new Date().toISOString().split('T')[0], Validators.required],
    dunningLevel: [1, [Validators.required, Validators.min(1)]],
    dunningFee: [50, [Validators.min(0)]],
    interestRate: [1.5, [Validators.min(0)]],
  });

  ngOnInit() {
    this.customerService.getList({ maxResultCount: 200 } as any).subscribe(res => {
      this.customers.set((res.items ?? []).map((c: any) => ({ id: c.id, name: c.customerName || c.name || c.id })));
    });
  }

  onCustomerChanged() {
    this.overdueInvoices.set([]);
    this.recalculate();
    if (this.form.value.customerId) {
      this.fetchOverdueInvoices();
    }
  }

  fetchOverdueInvoices() {
    const customerId = this.form.value.customerId;
    if (!customerId) return;
    this.fetchingInvoices.set(true);
    const companyId = this.companyContext.currentCompanyId();
    this.http.get<any>(`/api/app/payment-reconciliation/outstanding-invoices?partyType=Customer&partyId=${customerId}${companyId ? '&companyId=' + companyId : ''}`)
      .subscribe({
        next: (res) => {
          const today = new Date();
          const invoices: OverdueInvoice[] = (res ?? [])
            .filter((inv: any) => inv.outstandingAmount > 0)
            .map((inv: any) => {
              const dueDate = new Date(inv.dueDate || inv.issueDate);
              const overdueDays = Math.max(0, Math.floor((today.getTime() - dueDate.getTime()) / 86400000));
              return {
                id: inv.invoiceId || inv.id,
                invoiceNumber: inv.invoiceNumber || inv.voucherNumber || '—',
                dueDate: inv.dueDate || inv.issueDate,
                grandTotal: inv.grandTotal ?? 0,
                outstandingAmount: inv.outstandingAmount ?? 0,
                overdueDays,
                selected: overdueDays > 0,
              };
            })
            .filter((inv: OverdueInvoice) => inv.overdueDays > 0);
          this.overdueInvoices.set(invoices);
          this.recalculate();
          this.fetchingInvoices.set(false);
        },
        error: () => this.fetchingInvoices.set(false),
      });
  }

  calculateInterest(inv: OverdueInvoice): number {
    const rate = this.form.value.interestRate ?? 1.5;
    return (rate / 100 / 365) * inv.overdueDays * inv.outstandingAmount;
  }

  recalculate() {
    const selected = this.overdueInvoices().filter(i => i.selected);
    this.totalOutstanding.set(selected.reduce((s, i) => s + i.outstandingAmount, 0));
    this.totalInterest.set(selected.reduce((s, i) => s + this.calculateInterest(i), 0));
  }

  grandTotal(): number {
    return this.totalOutstanding() + this.totalInterest() + (this.form.value.dunningFee ?? 0);
  }

  toggleAll(event: Event) {
    const checked = (event.target as HTMLInputElement).checked;
    this.overdueInvoices.update(list => list.map(i => ({ ...i, selected: checked })));
    this.recalculate();
  }

  hasSelectedInvoices(): boolean {
    return this.overdueInvoices().some(i => i.selected);
  }

  save() {
    if (this.form.invalid || !this.hasSelectedInvoices()) return;
    this.saving = true;
    const v = this.form.value;
    const selected = this.overdueInvoices().filter(i => i.selected);
    const dto = {
      companyId: this.companyContext.currentCompanyId() || undefined,
      customerId: v.customerId || undefined,
      postingDate: v.postingDate,
      dunningLevel: v.dunningLevel ?? 1,
      dunningFee: v.dunningFee ?? 0,
      interestAmount: this.totalInterest(),
      overduePayments: selected.map(inv => ({
        salesInvoiceId: inv.id,
        outstandingAmount: inv.outstandingAmount,
        dueDate: inv.dueDate,
        overdueDays: inv.overdueDays,
      })),
    };
    this.service.create(dto).subscribe({
      next: () => { this.toaster.success('Dunning created successfully'); this.router.navigate(['/sales/dunnings']); },
      error: () => this.saving = false,
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saving; }
}