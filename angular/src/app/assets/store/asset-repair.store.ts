import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, removeEntity, updateEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';
import type { AssetRepairDto } from '../../proxy/assets/models';

type AssetRepairEntity = AssetRepairDto & { id: string };

export const AssetRepairStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<AssetRepairEntity>(),
  withMethods((store, service = inject(AssetRepairService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as AssetRepairEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('::FailedToLoad'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => { patchState(store, addEntity(created as AssetRepairEntity, { selectId: (e) => e.id! })); toaster.success('::SuccessfullyCreated'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? '::CreateFailed'); return EMPTY; }),
      )
    ),
    complete: rxMethod<string>(
      pipe(
        switchMap((id) => service.complete(id)),
        tap((updated) => { patchState(store, updateEntity({ id: (updated as AssetRepairEntity).id!, changes: updated as AssetRepairEntity })); toaster.success('::Completed'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? '::CompleteFailed'); return EMPTY; }),
      )
    ),
    cancel: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => { patchState(store, updateEntity({ id: (updated as AssetRepairEntity).id!, changes: updated as AssetRepairEntity })); toaster.success('::Cancelled'); }),
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
