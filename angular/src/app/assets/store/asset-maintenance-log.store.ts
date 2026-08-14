import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceLogService } from '../../proxy/assets/asset-maintenance-log.service';
import type {
  AssetMaintenanceLogDto,
  CompleteAssetMaintenanceLogDto,
  CreateUpdateAssetMaintenanceLogDto,
  GetAssetMaintenanceLogListDto,
} from '../../proxy/assets/models';

type AssetMaintenanceLogEntity = AssetMaintenanceLogDto & { id: string };

export const AssetMaintenanceLogStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<AssetMaintenanceLogEntity>(),
  withMethods((store, service = inject(AssetMaintenanceLogService), toaster = inject(ToasterService)) => ({
    load: rxMethod<GetAssetMaintenanceLogListDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as AssetMaintenanceLogEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => {
          patchState(store, { isLoading: false });
          toaster.error('::FailedToLoad');
          return EMPTY;
        }),
      )
    ),
    create: rxMethod<CreateUpdateAssetMaintenanceLogDto>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as AssetMaintenanceLogEntity, { selectId: (e) => e.id! }));
          toaster.success('::SuccessfullyCreated');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),
    complete: rxMethod<{ id: string; input: CompleteAssetMaintenanceLogDto }>(
      pipe(
        switchMap(({ id, input }) =>
          service.complete(id, input).pipe(
            tap((updated) => {
              patchState(store, updateEntity({ id, changes: updated as AssetMaintenanceLogEntity }));
              toaster.success('::MaintenanceCompleted');
            })
          )
        ),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Complete failed');
          return EMPTY;
        }),
      )
    ),
    cancel: rxMethod<string>(
      pipe(
        switchMap((id) =>
          service.cancel(id).pipe(
            tap((updated) => {
              patchState(store, updateEntity({ id, changes: updated as AssetMaintenanceLogEntity }));
              toaster.success('::MaintenanceCancelled');
            })
          )
        ),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),
    remove: rxMethod<string>(
      pipe(
        switchMap((id) =>
          service.delete(id).pipe(
            tap(() => {
              patchState(store, removeEntity(id));
              toaster.success('::SuccessfullyDeleted');
            })
          )
        ),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? '::DeleteFailed');
          return EMPTY;
        }),
      )
    ),
  })),
);
