import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import type { PurchaseReceiptDto, CreatePurchaseReceiptDto } from '../../proxy/purchasing/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type PurchaseReceiptEntity = PurchaseReceiptDto & { id: EntityId };

export interface PurchaseReceiptFilter {
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const PurchaseReceiptStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as PurchaseReceiptFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<PurchaseReceiptEntity>(),
  withComputed((store) => ({
    selectedReceipt: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasReceipts: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(PurchaseReceiptService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as PurchaseReceiptEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load purchase receipts');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreatePurchaseReceiptDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as PurchaseReceiptEntity));
          patchState(store, { isLoading: false });
          toaster.success('Purchase receipt created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitReceipt: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PurchaseReceiptEntity }));
          toaster.success('Purchase receipt submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    cancelReceipt: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PurchaseReceiptEntity }));
          toaster.success('Purchase receipt cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: PurchaseReceiptFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);