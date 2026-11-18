import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { CompanyService } from '../../../proxy/core/company.service';
import { CustomerGroupService } from '../../../proxy/core/customer-group.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';
import { SalesRegisterService } from '../../../proxy/sales/sales-register.service';
import { CustomerService } from '../../../proxy/sales/customer.service';
import type {
  ItemWiseSalesRegisterLineDto,
  RegisterReportDto,
  SalesRegisterLineDto,
  CustomerDto,
} from '../../../proxy/sales/models';
import type { CompanyDto, CustomerGroupDto } from '../../../proxy/core/models';

export function buildItemWiseSalesCsvRows(
  items: ItemWiseSalesRegisterLineDto[] | undefined,
): Record<string, string | number>[] {
  return (items ?? []).map(item => ({
    'Invoice Number': item.invoiceNumber ?? '',
    'Posting Date': item.postingDate ?? '',
    Customer: item.customerName ?? '',
    'Customer Group': item.customerGroupName ?? '',
    'Item Code': item.itemCode ?? '',
    'Item Name': item.itemName ?? '',
    'Item Group': item.itemGroup ?? '',
    Qty: item.stockQty ?? 0,
    UOM: item.stockUom ?? '',
    Rate: (item.rate ?? 0).toFixed(2),
    Amount: (item.amount ?? 0).toFixed(2),
    Tax: (item.totalTax ?? 0).toFixed(2),
    Total: (item.total ?? 0).toFixed(2),
    'SO Number': item.salesOrderNumber ?? '',
    'DN Number': item.deliveryNoteNumber ?? '',
  }));
}

@Component({
  selector: 'app-sales-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './sales-register.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./sales-register.component.scss'],
})
export class SalesRegisterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private reportService = inject(SalesRegisterService);
  private customerService = inject(CustomerService);
  private customerGroupService = inject(CustomerGroupService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    customerGroupId: [''],
    customerId: [''],
    itemCode: [''],
    fromDate: [new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().split('T')[0]],
    toDate: [new Date().toISOString().split('T')[0]],
    includePayments: [false],
  });

  reportMode: 'invoice' | 'item' = 'invoice';
  companies = signal<CompanyDto[]>([]);
  customerGroups = signal<CustomerGroupDto[]>([]);
  customers = signal<CustomerDto[]>([]);
  report = signal<RegisterReportDto<SalesRegisterLineDto> | null>(null);
  itemWiseReport = signal<RegisterReportDto<ItemWiseSalesRegisterLineDto> | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(res => {
        this.companies.set(res.items ?? []);
        const defaultId = this.companyContext.currentCompanyId();
        if (defaultId && !this.filters.get('companyId')?.value) {
          this.filters.patchValue({ companyId: defaultId });
        }
        if (this.filters.get('companyId')?.value) {
          this.generate();
        }
      });

    this.customerGroupService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe(res => {
        this.customerGroups.set(res.items ?? []);
      });

    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe(res => {
        this.customers.set(res.items ?? []);
      });
  }

  switchMode(mode: 'invoice' | 'item'): void {
    this.reportMode = mode;
    if (this.filters.valid) {
      this.generate();
    }
  }

  generate(): void {
    if (this.filters.invalid) { this.filters.markAllAsTouched(); return; }
    this.isLoading.set(true);
    const { companyId, fromDate, toDate, customerId, customerGroupId, itemCode, includePayments } = this.filters.getRawValue();
    if (this.reportMode === 'item') {
      this.reportService.getItemWiseReport({
        companyId: companyId!,
        fromDate: fromDate!,
        toDate: toDate!,
        customerId: customerId || undefined,
        customerGroupId: customerGroupId || undefined,
        itemCode: itemCode?.trim() || undefined,
      }).subscribe({
        next: data => { this.itemWiseReport.set(data); this.isLoading.set(false); },
        error: () => this.isLoading.set(false),
      });
      return;
    }

    this.reportService.getReport({
      companyId: companyId!,
      fromDate: fromDate!,
      toDate: toDate!,
      customerId: customerId || undefined,
      customerGroupId: customerGroupId || undefined,
      includePayments: includePayments || false,
    } as any).subscribe({
      next: data => { this.report.set(data); this.isLoading.set(false); },
      error: () => this.isLoading.set(false),
    });
  }

  exportCsv(): void {
    if (this.reportMode === 'item') {
      const ir = this.itemWiseReport();
      if (!ir?.items?.length) return;
      const rows = buildItemWiseSalesCsvRows(ir.items);
      exportToCsv('item-wise-sales-register.csv', rows, [
        'Invoice Number', 'Posting Date', 'Customer', 'Customer Group', 'Item Code', 'Item Name', 'Item Group',
        'Qty', 'UOM', 'Rate', 'Amount', 'Tax', 'Total', 'SO Number', 'DN Number',
      ]);
      return;
    }

    const r = this.report();
    if (!r?.items?.length) return;
    const isPayments = this.filters.get('includePayments')?.value;
    const cols = isPayments
      ? ['voucherType', 'invoiceNumber', 'postingDate', 'debit', 'credit', 'balance', 'outstanding']
      : ['invoiceNumber', 'postingDate', 'netTotal', 'taxAmount', 'grandTotal', 'amountPaid', 'outstanding', 'isReturn'];
    exportToCsv('sales-register.csv', r.items, cols);
  }
}
