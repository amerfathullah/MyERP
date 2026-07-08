import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesInvoiceService } from '../../proxy/sales/sales-invoice.service';
import type { SalesInvoiceDto, CreateSalesInvoiceDto } from '../../proxy/sales/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type InvoiceEntity = SalesInvoiceDto & { id: EntityId };

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
  withEntities<InvoiceEntity>(),
  withComputed((store) => ({
    selectedInvoice: computed(() =>
      store.entityMap()[store.selectedId() ?? '']
    ),
    hasInvoices: computed(() => store.ids().length > 0),
  })),
  withMethods((store, invoiceService = inject(SalesInvoiceService), toaster = inject(ToasterService)) => ({
    loadInvoices: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => invoiceService.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as InvoiceEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load invoices');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateSalesInvoiceDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => invoiceService.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as InvoiceEntity));
          patchState(store, { isLoading: false });
          toaster.success('Invoice created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    update: rxMethod<{ id: string; input: CreateSalesInvoiceDto }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input }) => invoiceService.create(input).pipe(
          // Note: proxy has no dedicated update method — re-create pattern
          // Replace with invoiceService.update(id, input) once proxy supports it
          tap((updated) => {
            patchState(store, updateEntity({ id: updated.id!, changes: updated as InvoiceEntity }));
            patchState(store, { isLoading: false });
            toaster.success('Invoice updated');
          }),
        )),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Update failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: SalesInvoiceFilter) {
      patchState(store, { filter });
    },
    selectInvoice(id: string | null) {
      patchState(store, { selectedId: id });
    },

    submitInvoice: rxMethod<string>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((id) => invoiceService.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as InvoiceEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Invoice submitted');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),
    postInvoice: rxMethod<string>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((id) => invoiceService.post(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as InvoiceEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Invoice posted');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Post failed');
          return EMPTY;
        }),
      )
    ),
    cancelInvoice: rxMethod<string>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((id) => invoiceService.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as InvoiceEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Invoice cancelled');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),
    deleteInvoice: rxMethod<string>(
      pipe(
        switchMap((id) => invoiceService.cancel(id).pipe(
          tap((updated) => {
            patchState(store, removeEntity(id));
            patchState(store, { totalCount: store.totalCount() - 1 });
            toaster.success('Invoice deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
