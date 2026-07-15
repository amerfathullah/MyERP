import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { OpportunityService } from '../../proxy/crm/opportunity.service';
import type { OpportunityDto } from '../../proxy/crm/models';
import type { OpportunityStatus } from '../../proxy/crm/opportunity-status.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface OpportunityFilter {
  status?: OpportunityStatus;
  filter?: string;
}

type OpportunityEntity = OpportunityDto & { id: string };

export const OpportunityStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as OpportunityFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
    error: null as string | null,
  }),
  withEntities<OpportunityEntity>(),
  withComputed((store) => ({
    selectedOpportunity: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasOpportunities: computed(() => store.ids().length > 0),
    totalPipelineValue: computed(() =>
      store.entities().reduce((sum, o) => sum + (o.opportunityAmount ?? 0), 0)
    ),
  })),
  withMethods((store, service = inject(OpportunityService), toaster = inject(ToasterService)) => ({

    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true, error: null })),
        switchMap((query) => service.getList({ ...query, ...store.filter() })),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as OpportunityEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false, error: err?.error?.error?.message ?? 'Load failed' });
          toaster.error('Failed to load opportunities');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as OpportunityEntity, { selectId: (e) => e.id! }));
          patchState(store, { isLoading: false });
          toaster.success('Opportunity created');
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
          patchState(store, updateEntity({ id: updated.id!, changes: updated as OpportunityEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Opportunity updated');
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
            toaster.success('Opportunity deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),

    convert: rxMethod<string>(
      pipe(
        switchMap((id) => service.convert(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as OpportunityEntity }));
          toaster.success('Opportunity converted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Convert failed');
          return EMPTY;
        }),
      )
    ),

    declareLost: rxMethod<{ id: string; reason?: string }>(
      pipe(
        switchMap(({ id, reason }) => service.declareLost(id, reason)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as OpportunityEntity }));
          toaster.success('Opportunity marked as lost');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Operation failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: OpportunityFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
