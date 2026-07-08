import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { computed } from '@angular/core';

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
  withMethods((store) => ({
    setLoading(isLoading: boolean) {
      patchState(store, { isLoading });
    },
    loadSuccess(sales: StatusCounts, purchase: StatusCounts, trend: MonthlyData[]) {
      patchState(store, {
        salesStats: sales,
        purchaseStats: purchase,
        monthlyTrend: trend,
        isLoading: false,
      });
    },
  })),
);
