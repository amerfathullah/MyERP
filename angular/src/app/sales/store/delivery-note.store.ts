import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { DeliveryNoteService } from '../../proxy/sales/delivery-note.service';
import type { DeliveryNoteDto, CreateDeliveryNoteDto } from '../../proxy/sales/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type DeliveryNoteEntity = DeliveryNoteDto & { id: EntityId };

export interface DeliveryNoteFilter {
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const DeliveryNoteStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as DeliveryNoteFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<DeliveryNoteEntity>(),
  withComputed((store) => ({
    selectedNote: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasNotes: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(DeliveryNoteService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as DeliveryNoteEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load delivery notes');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateDeliveryNoteDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as DeliveryNoteEntity));
          patchState(store, { isLoading: false });
          toaster.success('Delivery note created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitNote: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as DeliveryNoteEntity }));
          toaster.success('Delivery note submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    cancelNote: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as DeliveryNoteEntity }));
          toaster.success('Delivery note cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: DeliveryNoteFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
