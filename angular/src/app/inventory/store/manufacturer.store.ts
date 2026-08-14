import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY, map } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturerService } from '../../proxy/inventory/manufacturer.service';
import type { ManufacturerDto, CreateUpdateManufacturerDto } from '../../proxy/inventory/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type ManufacturerEntity = ManufacturerDto & { id: EntityId };

export const ManufacturerStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<ManufacturerEntity>(),
  withComputed((store) => ({
    selectedEntry: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasEntries: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(ManufacturerService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as ManufacturerEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? '::FailedToLoad');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateUpdateManufacturerDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as ManufacturerEntity));
          patchState(store, { isLoading: false });
          toaster.success('::SuccessfullyCreated');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    update: rxMethod<{ id: string; input: CreateUpdateManufacturerDto }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input }) => service.update(id, input)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id as EntityId, changes: updated as ManufacturerEntity }));
          patchState(store, { isLoading: false });
          toaster.success('::SuccessfullySaved');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Update failed');
          return EMPTY;
        }),
      )
    ),

    delete: rxMethod<string>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((id) => service.delete(id).pipe(map(() => id))),
        tap((id) => {
          patchState(store, removeEntity(id));
          patchState(store, { isLoading: false });
          toaster.success('::SuccessfullyDeleted');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
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
