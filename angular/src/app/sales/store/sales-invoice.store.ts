import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities } from '@ngrx/signals/entities';
import { computed } from '@angular/core';

export interface SalesInvoiceListItem {
  id: string;
  invoiceNumber: string;
  issueDate: string;
  customerName: string;
  grandTotal: number;
  status: string;
  eInvoiceStatus: string;
}

export interface SalesInvoiceFilter {
  status?: string;
  customerName?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const SalesInvoiceStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as SalesInvoiceFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<SalesInvoiceListItem>(),
  withComputed((store) => ({
    selectedInvoice: computed(() =>
      store.entityMap()[store.selectedId() ?? '']
    ),
    hasInvoices: computed(() => store.ids().length > 0),
  })),
  withMethods((store) => ({
    setLoading(isLoading: boolean) {
      patchState(store, { isLoading });
    },
    setFilter(filter: SalesInvoiceFilter) {
      patchState(store, { filter });
    },
    selectInvoice(id: string | null) {
      patchState(store, { selectedId: id });
    },
    loadSuccess(items: SalesInvoiceListItem[], totalCount: number) {
      patchState(store, setAllEntities(items));
      patchState(store, { totalCount, isLoading: false });
    },
  })),
);
