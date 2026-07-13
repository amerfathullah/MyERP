import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, removeEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';
import type { StockEntryDto } from '../../proxy/inventory/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type StockEntryEntity = StockEntryDto & { id: EntityId };

export const StockEntryStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<StockEntryEntity>(),
  withMethods((store, service = inject(StockEntryService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as StockEntryEntity[], { selectId: (e) => e.id }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load stock entries');
          return EMPTY;
        }),
      )
    ),
    submit: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as StockEntryEntity }));
          toaster.success('Stock entry submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),
    postEntry: rxMethod<string>(
      pipe(
        switchMap((id) => service.post(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as StockEntryEntity }));
          toaster.success('Stock entry posted');
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
          patchState(store, updateEntity({ id: updated.id!, changes: updated as StockEntryEntity }));
          toaster.success('Stock entry cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
