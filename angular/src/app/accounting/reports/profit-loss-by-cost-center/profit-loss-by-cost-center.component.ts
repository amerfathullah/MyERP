import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProfitLossByCostCenterService } from '../../../proxy/accounting/profit-loss-by-cost-center.service';
import { CompanyService } from '../../../proxy/core/company.service';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import type { ProfitLossByCostCenterDto, CostCenterPLRowDto } from '../../../proxy/accounting/models';
import type { CompanyDto } from '../../../proxy/core/models';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-profit-loss-by-cost-center',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './profit-loss-by-cost-center.component.html',
  styleUrls: ['./profit-loss-by-cost-center.component.scss'],
})
export class ProfitLossByCostCenterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ProfitLossByCostCenterService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  filters = this.fb.group({
    companyId: ['', Validators.required],
    fromDate: [new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0], Validators.required],
    toDate: [new Date().toISOString().split('T')[0], Validators.required],
  });

  companies = signal<CompanyDto[]>([]);
  report = signal<ProfitLossByCostCenterDto | null>(null);
  isLoading = signal(false);

  ngOnInit(): void {
    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe({
        next: res => {
          this.companies.set(res.items ?? []);
          const defaultId = this.companyContext.currentCompanyId();
          if (defaultId) {
            this.filters.patchValue({ companyId: defaultId });
            this.generate();
          }
        },
        error: () => {}
      });
  }

  generate(): void {
    if (this.filters.invalid) return;
    const { companyId, fromDate, toDate } = this.filters.value;
    this.isLoading.set(true);
    this.service.getReport(companyId!, fromDate!, toDate!).subscribe({
      next: data => {
        this.report.set(data);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('::FailedToLoad');
      }
    });
  }

  getMarginColor(margin: number | undefined): string {
    if (!margin) return 'text-muted';
    if (margin >= 20) return 'text-success';
    if (margin >= 0) return 'text-warning';
    return 'text-danger';
  }

  getBarWidth(revenue: number | undefined): number {
    const max = this.report()?.totalRevenue || 1;
    return Math.min(100, ((revenue || 0) / max) * 100);
  }

  exportCsv(): void {
    const data = this.report();
    if (!data?.costCenters?.length) return;
    const rows = data.costCenters.map(cc => ({
      'Cost Center': cc.costCenterName,
      'Revenue': cc.revenue?.toFixed(2),
      'Expense': cc.expense?.toFixed(2),
      'Net Profit': cc.netProfit?.toFixed(2),
      'Margin %': cc.profitMargin?.toFixed(1),
    }));
    rows.push({
      'Cost Center': 'TOTAL',
      'Revenue': data.totalRevenue?.toFixed(2) ?? '0.00',
      'Expense': data.totalExpense?.toFixed(2) ?? '0.00',
      'Net Profit': data.netProfit?.toFixed(2) ?? '0.00',
      'Margin %': data.overallMargin?.toFixed(1) ?? '0.0',
    });
    exportToCsv('pl-by-cost-center.csv', rows, ['Cost Center', 'Revenue', 'Expense', 'Net Profit', 'Margin %']);
  }
}
