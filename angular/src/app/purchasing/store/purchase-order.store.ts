import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { PurchaseOrderService } from '../../proxy/purchasing/purchase-order.service';
import type { PurchaseOrderDto, CreatePurchaseOrderDto } from '../../proxy/purchasing/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type PurchaseOrderEntity = PurchaseOrderDto & { id: EntityId };

export const PurchaseOrderStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<PurchaseOrderEntity>(),
  withComputed((store) => ({
    selectedOrder: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasOrders: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(PurchaseOrderService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as PurchaseOrderEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load purchase orders');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreatePurchaseOrderDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as PurchaseOrderEntity));
          patchState(store, { isLoading: false });
          toaster.success('Purchase order created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitOrder: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id! as EntityId, changes: updated as PurchaseOrderEntity }));
          toaster.success('Purchase order submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    cancelOrder: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id! as EntityId, changes: updated as PurchaseOrderEntity }));
          toaster.success('Purchase order cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id).pipe(tap(() => {
          patchState(store, removeEntity(id as EntityId));
          patchState(store, { totalCount: store.totalCount() - 1 });
          toaster.success('Deleted');
        }))),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),

    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
