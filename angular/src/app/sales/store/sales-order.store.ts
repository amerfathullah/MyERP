import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { SalesOrderService } from '../../proxy/sales/sales-order.service';
import type { SalesOrderDto, CreateSalesOrderDto } from '../../proxy/sales/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type SalesOrderEntity = SalesOrderDto & { id: EntityId };

export interface SalesOrderFilter {
  status?: string;
  customerName?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const SalesOrderStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as SalesOrderFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<SalesOrderEntity>(),
  withComputed((store) => ({
    selectedOrder: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasOrders: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(SalesOrderService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as SalesOrderEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load sales orders');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateSalesOrderDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as SalesOrderEntity));
          patchState(store, { isLoading: false });
          toaster.success('Sales order created');
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
          patchState(store, updateEntity({ id: updated.id!, changes: updated as SalesOrderEntity }));
          toaster.success('Sales order submitted');
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
          patchState(store, updateEntity({ id: updated.id!, changes: updated as SalesOrderEntity }));
          toaster.success('Sales order cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: SalesOrderFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
