import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { LeadService } from '../../proxy/crm/lead.service';
import type { LeadDto } from '../../proxy/crm/models';
import type { LeadStatus } from '../../proxy/crm/lead-status.enum';
import type { LeadSource } from '../../proxy/crm/lead-source.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface LeadFilter {
  status?: LeadStatus;
  source?: LeadSource;
  filter?: string;
}

type LeadEntity = LeadDto & { id: string };

export const LeadStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as LeadFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
    error: null as string | null,
  }),
  withEntities<LeadEntity>(),
  withComputed((store) => ({
    selectedLead: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasLeads: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(LeadService), toaster = inject(ToasterService)) => ({

    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true, error: null })),
        switchMap((query) => service.getList({ ...query, ...store.filter() })),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as LeadEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false, error: err?.error?.error?.message ?? 'Load failed' });
          toaster.error('Failed to load leads');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as LeadEntity, { selectId: (e) => e.id! }));
          patchState(store, { isLoading: false });
          toaster.success('Lead created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false, error: err?.error?.error?.message });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    update: rxMethod<{ id: string; input: any }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input }) => service.update(id, input)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as LeadEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Lead updated');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false, error: err?.error?.error?.message });
          toaster.error(err?.error?.error?.message ?? 'Update failed');
          return EMPTY;
        }),
      )
    ),

    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.delete(id).pipe(
          tap(() => {
            patchState(store, removeEntity(id));
            patchState(store, { totalCount: store.totalCount() - 1 });
            toaster.success('Lead deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),

    qualify: rxMethod<string>(
      pipe(
        switchMap((id) => service.qualify(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as LeadEntity }));
          toaster.success('Lead qualified');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Qualify failed');
          return EMPTY;
        }),
      )
    ),

    markLost: rxMethod<string>(
      pipe(
        switchMap((id) => service.markLost(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as LeadEntity }));
          toaster.success('Lead marked as lost');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Operation failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: LeadFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
