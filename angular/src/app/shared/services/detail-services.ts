import { Injectable, inject } from '@angular/core';
import { BudgetService } from '../../proxy/accounting/budget.service';
import { LandedCostVoucherService } from '../../proxy/inventory/landed-cost-voucher.service';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';
import { StockReconciliationService } from '../../proxy/inventory/stock-reconciliation.service';
import { HolidayListService } from '../../proxy/human-resources/holiday-list.service';
import { IssueService } from '../../proxy/support/issue.service';

@Injectable({ providedIn: 'root' })
export class BudgetDetailService {
  private budgetService = inject(BudgetService);
  get = (id: string) => this.budgetService.get(id);
}

@Injectable({ providedIn: 'root' })
export class LandedCostDetailService {
  private landedCostVoucherService = inject(LandedCostVoucherService);
  get = (id: string) => this.landedCostVoucherService.get(id);
}

@Injectable({ providedIn: 'root' })
export class QualityInspectionDetailService {
  private qualityInspectionService = inject(QualityInspectionService);
  get = (id: string) => this.qualityInspectionService.get(id);
}

@Injectable({ providedIn: 'root' })
export class StockReconciliationDetailService {
  private stockReconciliationService = inject(StockReconciliationService);
  get = (id: string) => this.stockReconciliationService.get(id);
}

@Injectable({ providedIn: 'root' })
export class HolidayListDetailService {
  private holidayListService = inject(HolidayListService);
  get = (id: string) => this.holidayListService.get(id);
}

@Injectable({ providedIn: 'root' })
export class IssueDetailService {
  private issueService = inject(IssueService);
  get = (id: string) => this.issueService.get(id);
  reply = (id: string) => this.issueService.reply(id);
  resolve = (id: string) => this.issueService.resolve(id);
}
