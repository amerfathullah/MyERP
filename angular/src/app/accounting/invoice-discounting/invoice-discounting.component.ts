import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { InvoiceDiscountingService } from '../../proxy/accounting/invoice-discounting.service';
import { RestService } from '@abp/ng.core';

const STATUS = ['Draft', 'Sanctioned', 'Disbursed', 'Settled', 'Cancelled'] as const;

@Component({
  selector: 'app-invoice-discounting',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'InvoiceDiscounting' | abpLocalization">
      <!-- Calculate Section -->
      <div class="card mb-3"><div class="card-body">
        <h6 class="card-title">Calculate Discount</h6>
        <div class="row g-3">
          <div class="col-md-4">
            <label class="form-label">Invoice Amount</label>
            <input type="number" class="form-control" [(ngModel)]="calcInput.invoiceAmount" step="0.01" />
          </div>
          <div class="col-md-3">
            <label class="form-label">Discount Rate (%)</label>
            <input type="number" class="form-control" [(ngModel)]="calcInput.discountRate" step="0.01" />
          </div>
          <div class="col-md-3">
            <label class="form-label">Period (Days)</label>
            <input type="number" class="form-control" [(ngModel)]="calcInput.periodDays" />
          </div>
          <div class="col-md-2 d-flex align-items-end">
            <button class="btn btn-primary btn-sm" (click)="calculate()">Calculate</button>
          </div>
        </div>
        @if (calcResult) {
          <div class="alert alert-info mt-3 mb-0">
            <div class="row">
              <div class="col-md-4"><strong>Discount Charge:</strong> {{ calcResult.discountCharge | number:'1.2-2' }}</div>
              <div class="col-md-4"><strong>Net Disbursement:</strong> {{ calcResult.netDisbursement | number:'1.2-2' }}</div>
              <div class="col-md-4"><strong>Effective Rate:</strong> {{ calcResult.effectiveRate | number:'1.2-2' }}% p.a.</div>
            </div>
          </div>
        }
      </div></div>

      <!-- History -->
      @if (isLoading) { <app-loading-overlay /> }
      @if (!isLoading && items.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-percent fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoInvoiceDiscountingYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body p-0">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
              <th class="text-end">Discount Charge</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (item of items; track item.id) {
                <tr>
                  <td>{{ item.postingDate | date:'dd/MM/yyyy' }}</td>
                  <td class="text-end">{{ item.totalAmount | number:'1.2-2' }}</td>
                  <td class="text-end">{{ item.discountCharge | number:'1.2-2' }}</td>
                  <td><span class="badge" [ngClass]="statusClass(item.status)">{{ STATUS[item.status ?? 0] }}</span></td>
                  <td>
                    <div class="btn-group btn-group-sm">
                      @if (item.status === 1) {
                        <button class="btn btn-outline-success" (click)="disburse(item.id)" title="Disburse">
                          <i class="fa fa-money-bill-wave"></i>
                        </button>
                      }
                      @if (item.status === 2) {
                        <button class="btn btn-outline-primary" (click)="settle(item.id)" title="Settle">
                          <i class="fa fa-handshake"></i>
                        </button>
                      }
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `
})
export class InvoiceDiscountingComponent implements OnInit {
  private invoiceDiscountingService = inject(InvoiceDiscountingService);
  private restService = inject(RestService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  items: any[] = [];
  isLoading = false;
  STATUS = STATUS;
  calcInput = { invoiceAmount: 0, discountRate: 0, periodDays: 90 };
  calcResult: any = null;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    const cid = this.companyContext.currentCompanyId();
    const params: any = { maxResultCount: '50' };
    if (cid) params.companyId = cid;
    this.restService.request<any, any>({ method: 'GET', url: '/api/app/invoice-discounting', params }, { apiName: 'Default' }).subscribe({
      next: res => { this.items = res.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  calculate() {
    this.invoiceDiscountingService.calculate(this.calcInput as any).subscribe({
      next: res => { this.calcResult = res; },
      error: () => {}
    });
  }

  disburse(id: string) {
    this.restService.request<any, void>({ method: 'POST', url: `/api/app/invoice-discounting/${id}/disburse` }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Disbursed'); this.loadData(); },
      error: () => {}
    });
  }

  settle(id: string) {
    this.restService.request<any, void>({ method: 'POST', url: `/api/app/invoice-discounting/${id}/settle` }, { apiName: 'Default' }).subscribe({
      next: () => { this.toaster.success('Settled'); this.loadData(); },
      error: () => {}
    });
  }

  statusClass(status: number): string {
    return ['bg-warning text-dark', 'bg-info', 'bg-primary', 'bg-success', 'bg-secondary'][status ?? 0];
  }
}
