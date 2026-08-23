import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { EInvoiceService } from '../../proxy/einvoice/einvoice.service';

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
  withMethods((store, eInvoiceService = inject(EInvoiceService), toaster = inject(ToasterService)) => ({
    loadDashboard: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(() => eInvoiceService.getDashboardStats('')),
        tap((stats) => {
          patchState(store, {
            salesStats: {
              valid: stats.salesValid ?? 0,
              invalid: stats.salesInvalid ?? 0,
              submitted: stats.salesSubmitted ?? 0,
              cancelled: stats.salesCancelled ?? 0,
              failed: stats.salesFailed ?? 0,
              notSubmitted: stats.salesNotSubmitted ?? 0,
            },
            purchaseStats: {
              valid: stats.purchaseValid ?? 0,
              invalid: stats.purchaseInvalid ?? 0,
              submitted: stats.purchaseSubmitted ?? 0,
              cancelled: stats.purchaseCancelled ?? 0,
              failed: stats.purchaseFailed ?? 0,
              notSubmitted: stats.purchaseNotSubmitted ?? 0,
            },
            isLoading: false,
          });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error('::FailedToLoad');
          return EMPTY;
        }),
      )
    ),
    loadMonthlyTrend: rxMethod<void>(
      pipe(
        switchMap(() => eInvoiceService.getMonthlyTrend('')),
        tap((trend) => {
          patchState(store, {
            monthlyTrend: trend.map((m) => ({
              month: m.month ?? '',
              valid: m.valid ?? 0,
              invalid: m.invalid ?? 0,
              submitted: m.submitted ?? 0,
            })),
          });
        }),
        catchError(() => {
          toaster.error('::FailedToLoad');
          return EMPTY;
        }),
      )
    ),
  })),
);

