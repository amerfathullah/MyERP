import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';

interface UnbilledSummary {
  activityType: string;
  totalHours: number;
  totalAmount: number;
  entryCount: number;
}

interface CustomerOption {
  id: string;
  customerName: string;
}

@Component({
  selector: 'app-timesheet-billing',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './timesheet-billing.component.html',
  styleUrls: ['./timesheet-billing.component.scss'],
})
export class TimesheetBillingComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private customerService = inject(CustomerService);

  unbilledItems = signal<UnbilledSummary[]>([]);
  customers = signal<CustomerOption[]>([]);
  isLoading = signal(false);
  isBilling = signal(false);
  customerId = '';
  projectId = '';
  totalHours = signal(0);
  totalAmount = signal(0);

  ngOnInit(): void {
    this.loadCustomers();
    this.loadUnbilled();
  }

  loadCustomers(): void {
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: (result: any) => {
        this.customers.set((result.items ?? []).map((c: any) => ({ id: c.id, customerName: c.customerName })));
      },
    });
  }

  loadUnbilled(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;

    this.isLoading.set(true);
    this.http.get<UnbilledSummary[]>('/api/app/timesheet/unbilled-summary', {
      params: { companyId, ...(this.projectId ? { projectId: this.projectId } : {}) },
    }).subscribe({
      next: (items) => {
        this.unbilledItems.set(items);
        this.totalHours.set(items.reduce((s, i) => s + i.totalHours, 0));
        this.totalAmount.set(items.reduce((s, i) => s + i.totalAmount, 0));
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('Failed to load unbilled timesheets');
      },
    });
  }

  createInvoice(): void {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId || !this.customerId) {
      this.toaster.warn('Please select a company and customer');
      return;
    }

    this.isBilling.set(true);
    this.http.post<any>('/api/app/timesheet/create-invoice-from-timesheets', {
      companyId,
      customerId: this.customerId,
      projectId: this.projectId || undefined,
    }).subscribe({
      next: (result) => {
        this.isBilling.set(false);
        this.toaster.success(`Invoice ${result.invoiceNumber} created (${result.totalHours}h, MYR ${result.totalAmount})`);
        this.router.navigate(['/sales/invoices', result.invoiceId]);
      },
      error: (err) => {
        this.isBilling.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Failed to create invoice');
      },
    });
  }
}
