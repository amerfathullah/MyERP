import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';
import type { QualityReviewDto, CreateQualityReviewDto, EvaluateQualityReviewDto } from '../../proxy/inventory/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type QualityReviewEntity = QualityReviewDto & { id: EntityId };

export const QualityReviewStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<QualityReviewEntity>(),
  withComputed((store) => ({
    selectedEntry: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasEntries: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(QualityManagementService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getReviewList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as QualityReviewEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? '::FailedToLoad');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<{ input: CreateQualityReviewDto; onSuccess?: () => void }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ input, onSuccess }) =>
          service.createReview(input).pipe(
            tap((created) => {
              patchState(store, addEntity(created as QualityReviewEntity));
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

    evaluate: rxMethod<{ id: string; input: EvaluateQualityReviewDto; onSuccess?: () => void }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input, onSuccess }) =>
          service.evaluateReview(id, input).pipe(
            tap((updated) => {
              patchState(store, updateEntity({ id, changes: updated as QualityReviewEntity }));
              patchState(store, { isLoading: false });
              toaster.success('::SuccessfullySaved');
              onSuccess?.();
            }),
            catchError((err) => {
              patchState(store, { isLoading: false });
              toaster.error(err?.error?.error?.message ?? 'Evaluation failed');
              return EMPTY;
            })
          )
        )
      )
    ),
  }))
);
