import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, removeEntity, updateEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetValueAdjustmentService } from '../../proxy/assets/asset-value-adjustment.service';
import type { AssetValueAdjustmentDto } from '../../proxy/assets/models';

type AssetValueAdjustmentEntity = AssetValueAdjustmentDto & { id: string };

export const AssetValueAdjustmentStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<AssetValueAdjustmentEntity>(),
  withMethods((store, service = inject(AssetValueAdjustmentService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as AssetValueAdjustmentEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('::FailedToLoad'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => { patchState(store, addEntity(created as AssetValueAdjustmentEntity, { selectId: (e) => e.id! })); toaster.success('::SuccessfullyCreated'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? '::CreateFailed'); return EMPTY; }),
      )
    ),
    submit: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => { patchState(store, updateEntity({ id: (updated as AssetValueAdjustmentEntity).id!, changes: updated as AssetValueAdjustmentEntity })); toaster.success('::Submitted'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? '::SubmitFailed'); return EMPTY; }),
      )
    ),
    cancel: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => { patchState(store, updateEntity({ id: (updated as AssetValueAdjustmentEntity).id!, changes: updated as AssetValueAdjustmentEntity })); toaster.success('::Cancelled'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? '::CancelFailed'); return EMPTY; }),
      )
    ),
    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.delete(id).pipe(tap(() => { patchState(store, removeEntity(id)); toaster.success('::SuccessfullyDeleted'); }))),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? '::DeleteFailed'); return EMPTY; }),
      )
    ),
  })),
);
