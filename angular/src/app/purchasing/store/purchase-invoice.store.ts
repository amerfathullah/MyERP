import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities } from '@ngrx/signals/entities';
import { computed } from '@angular/core';

export interface PurchaseInvoiceListItem {
  id: string;
  invoiceNumber: string;
  issueDate: string;
  supplierName: string;
  grandTotal: number;
  status: string;
  eInvoiceStatus: string;
}

export interface PurchaseInvoiceFilter {
  status?: string;
  supplierName?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const PurchaseInvoiceStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as PurchaseInvoiceFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<PurchaseInvoiceListItem>(),
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
    setFilter(filter: PurchaseInvoiceFilter) {
      patchState(store, { filter });
    },
    selectInvoice(id: string | null) {
      patchState(store, { selectedId: id });
    },
    loadSuccess(items: PurchaseInvoiceListItem[], totalCount: number) {
      patchState(store, setAllEntities(items));
      patchState(store, { totalCount, isLoading: false });
    },
  })),
);
