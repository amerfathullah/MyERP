import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import type { PurchaseInvoiceDto, CreatePurchaseInvoiceDto } from '../../proxy/purchasing/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type PurchaseInvoiceEntity = PurchaseInvoiceDto & { id: EntityId };

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
  withEntities<PurchaseInvoiceEntity>(),
  withComputed((store) => ({
    selectedInvoice: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasInvoices: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(PurchaseInvoiceService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as PurchaseInvoiceEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load purchase invoices');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreatePurchaseInvoiceDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as PurchaseInvoiceEntity));
          patchState(store, { isLoading: false });
          toaster.success('Purchase invoice created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitInvoice: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PurchaseInvoiceEntity }));
          toaster.success('Purchase invoice submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    postInvoice: rxMethod<string>(
      pipe(
        switchMap((id) => service.post(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PurchaseInvoiceEntity }));
          toaster.success('Purchase invoice posted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Post failed');
          return EMPTY;
        }),
      )
    ),

    cancelInvoice: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PurchaseInvoiceEntity }));
          toaster.success('Purchase invoice cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: PurchaseInvoiceFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);