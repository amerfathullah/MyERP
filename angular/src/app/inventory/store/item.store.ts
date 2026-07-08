import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ItemService } from '../../proxy/inventory/item.service';
import type { ItemDto, CreateUpdateItemDto } from '../../proxy/inventory/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type ItemEntity = ItemDto & { id: EntityId };

export interface ItemFilter {
  itemGroup?: string;
  search?: string;
}

export const ItemStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as ItemFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<ItemEntity>(),
  withComputed((store) => ({
    selectedItem: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasItems: computed(() => store.ids().length > 0),
  })),
  withMethods((store, itemService = inject(ItemService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => itemService.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as ItemEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load items');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateUpdateItemDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => itemService.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as ItemEntity));
          patchState(store, { isLoading: false });
          toaster.success('Item created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    update: rxMethod<{ id: string; input: CreateUpdateItemDto }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input }) => itemService.update(id, input)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as ItemEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Item updated');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Update failed');
          return EMPTY;
        }),
      )
    ),

    remove: rxMethod<string>(
      pipe(
        switchMap((id) => itemService.delete(id).pipe(
          tap(() => {
            patchState(store, removeEntity(id));
            patchState(store, { totalCount: store.totalCount() - 1 });
            toaster.success('Item deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: ItemFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
