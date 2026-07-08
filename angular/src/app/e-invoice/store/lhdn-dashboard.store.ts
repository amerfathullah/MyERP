import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY, forkJoin, map } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import type { EInvoiceSubmissionDto } from '../../proxy/einvoice/models';
import type { SalesInvoiceDto } from '../../proxy/sales/models';
import type { PurchaseInvoiceDto } from '../../proxy/purchasing/models';

export interface StatusCounts {
  valid: number;
  invalid: number;
  submitted: number;
  cancelled: number;
  failed: number;
  notSubmitted: number;
}

export interface MonthlyData {
  month: string;
  valid: number;
  invalid: number;
  submitted: number;
}

interface DashboardState {
  salesStats: StatusCounts;
  purchaseStats: StatusCounts;
  monthlyTrend: MonthlyData[];
  isLoading: boolean;
}

const initialState: DashboardState = {
  salesStats: { valid: 0, invalid: 0, submitted: 0, cancelled: 0, failed: 0, notSubmitted: 0 },
  purchaseStats: { valid: 0, invalid: 0, submitted: 0, cancelled: 0, failed: 0, notSubmitted: 0 },
  monthlyTrend: [],
  isLoading: false,
};

function deriveStatsFromSales(invoices: SalesInvoiceDto[]): StatusCounts {
  const counts: StatusCounts = { valid: 0, invalid: 0, submitted: 0, cancelled: 0, failed: 0, notSubmitted: 0 };
  for (const inv of invoices) {
    switch (inv.eInvoiceStatus) {
      case 'Valid': counts.valid++; break;
      case 'Invalid': counts.invalid++; break;
      case 'Submitted': counts.submitted++; break;
      case 'Cancelled': counts.cancelled++; break;
      case 'Failed': counts.failed++; break;
      default: counts.notSubmitted++; break;
    }
  }
  return counts;
}

function deriveStatsFromPurchases(invoices: PurchaseInvoiceDto[]): StatusCounts {
  const counts: StatusCounts = { valid: 0, invalid: 0, submitted: 0, cancelled: 0, failed: 0, notSubmitted: 0 };
  for (const inv of invoices) {
    switch (inv.eInvoiceStatus) {
      case 'Valid': counts.valid++; break;
      case 'Invalid': counts.invalid++; break;
      case 'Submitted': counts.submitted++; break;
      case 'Cancelled': counts.cancelled++; break;
      case 'Failed': counts.failed++; break;
      default: counts.notSubmitted++; break;
    }
  }
  return counts;
}

export const LhdnDashboardStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ salesStats, purchaseStats }) => ({
    totalSalesSubmissions: computed(() =>
      salesStats().valid + salesStats().invalid + salesStats().submitted
    ),
    totalPurchaseSubmissions: computed(() =>
      purchaseStats().valid + purchaseStats().invalid + purchaseStats().submitted
    ),
    salesSuccessRate: computed(() => {
      const total = salesStats().valid + salesStats().invalid;
      return total > 0 ? Math.round((salesStats().valid / total) * 100) : 0;
    }),
  })),
  withMethods((store, eInvoiceService = inject(EInvoiceService), salesService = inject(SalesInvoiceService), purchaseService = inject(PurchaseInvoiceService), toaster = inject(ToasterService)) => ({
    loadDashboard: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(() => forkJoin({
          sales: salesService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }),
          purchases: purchaseService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' }),
        })),
        tap(({ sales, purchases }) => {
          patchState(store, {
            salesStats: deriveStatsFromSales(sales.items ?? []),
            purchaseStats: deriveStatsFromPurchases(purchases.items ?? []),
            isLoading: false,
          });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error('Failed to load dashboard data');
          return EMPTY;
        }),
      )
    ),
  })),
);
