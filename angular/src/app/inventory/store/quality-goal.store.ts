import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityGoalDto, CreateUpdateQualityGoalDto } from '../../proxy/inventory/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type QualityGoalEntity = QualityGoalDto & { id: EntityId };

export const QualityGoalStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<QualityGoalEntity>(),
  withComputed((store) => ({
    selectedEntry: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasEntries: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(QualityManagementService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getGoalList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as QualityGoalEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? '::FailedToLoad');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<{ input: CreateUpdateQualityGoalDto; onSuccess?: () => void }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ input, onSuccess }) =>
          service.createGoal(input).pipe(
            tap((created) => {
              patchState(store, addEntity(created as QualityGoalEntity));
              patchState(store, { isLoading: false });
              toaster.success('::SuccessfullyCreated');
              onSuccess?.();
            }),
            catchError((err) => {
              patchState(store, { isLoading: false });
              toaster.error(err?.error?.error?.message ?? 'Create failed');
              return EMPTY;
            })
          )
        )
      )
    ),

    update: rxMethod<{ id: string; input: CreateUpdateQualityGoalDto; onSuccess?: () => void }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input, onSuccess }) =>
          service.updateGoal(id, input).pipe(
            tap((updated) => {
              patchState(store, updateEntity({ id, changes: updated as QualityGoalEntity }));
              patchState(store, { isLoading: false });
              toaster.success('::SuccessfullySaved');
              onSuccess?.();
            }),
            catchError((err) => {
              patchState(store, { isLoading: false });
              toaster.error(err?.error?.error?.message ?? 'Update failed');
              return EMPTY;
            })
          )
        )
      )
    ),

    remove: rxMethod<{ id: string; onSuccess?: () => void }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, onSuccess }) =>
          service.deleteGoal(id).pipe(
            tap(() => {
              patchState(store, removeEntity(id));
              patchState(store, { isLoading: false });
              toaster.success('::SuccessfullyDeleted');
              onSuccess?.();
            }),
            catchError((err) => {
              patchState(store, { isLoading: false });
              toaster.error(err?.error?.error?.message ?? 'Delete failed');
              return EMPTY;
            })
          )
        )
      )
    ),
  }))
);
