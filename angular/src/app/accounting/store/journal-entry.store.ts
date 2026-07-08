import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { JournalEntryService } from '../../proxy/accounting/journal-entry.service';
import type { JournalEntryDto, CreateJournalEntryDto } from '../../proxy/accounting/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type JournalEntryEntity = JournalEntryDto & { id: EntityId };

export interface JournalEntryFilter {
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const JournalEntryStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as JournalEntryFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<JournalEntryEntity>(),
  withComputed((store) => ({
    selectedEntry: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasEntries: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(JournalEntryService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as JournalEntryEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load journal entries');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateJournalEntryDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as JournalEntryEntity));
          patchState(store, { isLoading: false });
          toaster.success('Journal entry created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    postEntry: rxMethod<string>(
      pipe(
        switchMap((id) => service.post(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as JournalEntryEntity }));
          toaster.success('Journal entry posted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Post failed');
          return EMPTY;
        }),
      )
    ),

    cancelEntry: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as JournalEntryEntity }));
          toaster.success('Journal entry cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: JournalEntryFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
