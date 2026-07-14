import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface OutstandingInvoice {
  invoiceId: string;
  invoiceNumber: string;
  invoiceType: string;
  partyId: string;
  issueDate: string;
  dueDate: string;
  grandTotal: number;
  outstanding: number;
  currencyCode: string;
  selected?: boolean;
  payAmount?: number;
}

@Component({
  selector: 'app-batch-payment',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'BatchPayment' | abpLocalization">
      <!-- Configuration -->
      <div class="card mb-4">
        <div class="card-header">
          <h6 class="card-title mb-0"><i class="fa fa-cogs me-2"></i>{{ 'PaymentConfiguration' | abpLocalization }}</h6>
        </div>
        <div class="card-body">
          <div class="row g-3">
            <div class="col-md-3">
              <label class="form-label">{{ 'PartyType' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="partyType" (ngModelChange)="onPartyTypeChange()">
                <option value="Supplier">Supplier</option>
                <option value="Customer">Customer</option>
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'Party' | abpLocalization }}</label>
              <select class="form-select" [(ngModel)]="selectedPartyId" (ngModelChange)="loadOutstanding()">
                <option value="">{{ 'SelectParty' | abpLocalization }}</option>
                @for (p of parties(); track p.id) { <option [value]="p.id">{{ p.name }}</option> }
              </select>
            </div>
            <div class="col-md-3">
              <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
              <input type="date" class="form-control" [(ngModel)]="postingDate" />
            </div>
            <div class="col-md-3">
              <div class="form-check mt-4">
                <input class="form-check-input" type="checkbox" [(ngModel)]="groupByParty" id="groupByParty">
                <label class="form-check-label" for="groupByParty">{{ 'GroupByParty' | abpLocalization }}</label>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Outstanding Invoices -->
      <div class="card mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h6 class="card-title mb-0"><i class="fa fa-file-invoice me-2"></i>{{ 'OutstandingInvoices' | abpLocalization }}</h6>
          @if (selectedInvoices().length > 0) {
            <span class="badge bg-primary">{{ selectedInvoices().length }} selected — {{ 'Total' | abpLocalization }}: {{ totalPayAmount() | number:'1.2-2' }}</span>
          }
        </div>
        <div class="card-body">
          @if (loading()) {
            <div class="text-center p-4"><i class="fa fa-spinner fa-spin"></i></div>
          } @else if (!selectedPartyId) {
            <div class="text-center text-muted p-4">
              <i class="fa fa-hand-pointer fa-2x mb-2 d-block"></i>
              {{ 'SelectPartyToViewOutstanding' | abpLocalization }}
            </div>
          } @else if (invoices().length === 0) {
            <div class="text-center text-muted p-4">
              <i class="fa fa-check-circle fa-2x mb-2 d-block text-success"></i>
              {{ 'NoOutstandingInvoices' | abpLocalization }}
            </div>
          } @else {
            <table class="table table-hover">
              <thead>
                <tr>
                  <th><input type="checkbox" (change)="toggleAll($event)" /></th>
                  <th>{{ 'InvoiceNumber' | abpLocalization }}</th>
                  <th>{{ 'IssueDate' | abpLocalization }}</th>
                  <th>{{ 'DueDate' | abpLocalization }}</th>
                  <th class="text-end">{{ 'GrandTotal' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Outstanding' | abpLocalization }}</th>
                  <th class="text-end">{{ 'PayAmount' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (inv of invoices(); track inv.invoiceId) {
                  <tr [class.table-active]="inv.selected">
                    <td><input type="checkbox" [(ngModel)]="inv.selected" (ngModelChange)="updateSelection()" /></td>
                    <td>{{ inv.invoiceNumber }}</td>
                    <td>{{ inv.issueDate | date:'dd/MM/yyyy' }}</td>
                    <td>
                      {{ inv.dueDate | date:'dd/MM/yyyy' }}
                      @if (isOverdue(inv)) { <span class="badge bg-danger ms-1">Overdue</span> }
                    </td>
                    <td class="text-end">{{ inv.grandTotal | number:'1.2-2' }}</td>
                    <td class="text-end">{{ inv.outstanding | number:'1.2-2' }}</td>
                    <td class="text-end" style="width: 150px;">
                      <input type="number" class="form-control form-control-sm text-end"
                        [(ngModel)]="inv.payAmount" [max]="inv.outstanding" [min]="0"
                        [disabled]="!inv.selected" (ngModelChange)="updateSelection()" />
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>

      <!-- Submit -->
      @if (selectedInvoices().length > 0) {
        <div class="card">
          <div class="card-body d-flex justify-content-between align-items-center">
            <div>
              <strong>{{ selectedInvoices().length }}</strong> invoices selected,
              total payment: <strong>{{ totalPayAmount() | number:'1.2-2' }} {{ invoices()[0]?.currencyCode }}</strong>
            </div>
            <button class="btn btn-primary btn-lg" (click)="createBatchPayment()" [disabled]="processing()">
              @if (processing()) { <i class="fa fa-spinner fa-spin me-2"></i> }
              <i class="fa fa-paper-plane me-1"></i>{{ 'CreatePaymentEntries' | abpLocalization }}
            </button>
          </div>
        </div>
      }
    </abp-page>
  `
})
export class BatchPaymentComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  partyType = 'Supplier';
  selectedPartyId = '';
  postingDate = new Date().toISOString().split('T')[0];
  groupByParty = true;

  parties = signal<{ id: string; name: string }[]>([]);
  invoices = signal<OutstandingInvoice[]>([]);
  loading = signal(false);
  processing = signal(false);

  selectedInvoices = signal<OutstandingInvoice[]>([]);
  totalPayAmount = signal(0);

  ngOnInit() {
    this.loadParties();
  }

  onPartyTypeChange() {
    this.selectedPartyId = '';
    this.invoices.set([]);
    this.loadParties();
  }

  loadParties() {
    const endpoint = this.partyType === 'Supplier' ? '/api/app/supplier' : '/api/app/customer';
    this.http.get<any>(endpoint, { params: { maxResultCount: '200' } }).subscribe({
      next: (res) => {
        const items = res.items ?? res ?? [];
        this.parties.set(items.map((p: any) => ({
          id: p.id,
          name: p.supplierName ?? p.customerName ?? p.name ?? ''
        })));
      }
    });
  }

  loadOutstanding() {
    if (!this.selectedPartyId) {
      this.invoices.set([]);
      return;
    }
    this.loading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    this.http.post<OutstandingInvoice[]>('/api/app/batch-payment/outstanding-invoices', {
      companyId,
      partyType: this.partyType,
      partyId: this.selectedPartyId
    }).subscribe({
      next: (res) => {
        const invoices = (res ?? []).map(inv => ({ ...inv, selected: false, payAmount: inv.outstanding }));
        this.invoices.set(invoices);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  toggleAll(event: any) {
    const checked = event.target.checked;
    this.invoices.update(invs => invs.map(inv => ({ ...inv, selected: checked })));
    this.updateSelection();
  }

  updateSelection() {
    const selected = this.invoices().filter(i => i.selected && (i.payAmount ?? 0) > 0);
    this.selectedInvoices.set(selected);
    this.totalPayAmount.set(selected.reduce((sum, i) => sum + (i.payAmount ?? 0), 0));
  }

  isOverdue(inv: OutstandingInvoice): boolean {
    if (!inv.dueDate) return false;
    return new Date(inv.dueDate) < new Date();
  }

  createBatchPayment() {
    const selected = this.selectedInvoices();
    if (!selected.length) return;

    this.processing.set(true);
    const companyId = this.companyContext.currentCompanyId();

    this.http.post<any>('/api/app/batch-payment/create-batch-payment', {
      companyId,
      partyType: this.partyType,
      paymentType: this.partyType === 'Supplier' ? 1 : 0, // Pay=1, Receive=0
      paidFromAccountId: '00000000-0000-0000-0000-000000000000', // TODO: from company defaults
      paidToAccountId: '00000000-0000-0000-0000-000000000000', // TODO: from company defaults
      postingDate: this.postingDate,
      groupByParty: this.groupByParty,
      items: selected.map(inv => ({
        partyId: inv.partyId,
        invoiceId: inv.invoiceId,
        invoiceType: inv.invoiceType,
        totalAmount: inv.grandTotal,
        outstanding: inv.outstanding,
        amount: inv.payAmount ?? inv.outstanding,
        exchangeRate: 1
      }))
    }).subscribe({
      next: (res) => {
        this.toaster.success(`${res.successCount} payment entries created (${res.totalAmount.toFixed(2)})`);
        this.processing.set(false);
        this.loadOutstanding(); // Refresh to remove paid invoices
      },
      error: () => this.processing.set(false)
    });
  }
}
