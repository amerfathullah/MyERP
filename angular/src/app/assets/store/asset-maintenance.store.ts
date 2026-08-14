import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetMaintenanceService } from '../../proxy/assets/asset-maintenance.service';
import type {
  AssetMaintenanceDto,
  CreateUpdateAssetMaintenanceDto,
  GetAssetMaintenanceListDto,
} from '../../proxy/assets/models';

type AssetMaintenanceEntity = AssetMaintenanceDto & { id: string };

export const AssetMaintenanceStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<AssetMaintenanceEntity>(),
  withMethods((store, service = inject(AssetMaintenanceService), toaster = inject(ToasterService)) => ({
    load: rxMethod<GetAssetMaintenanceListDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as AssetMaintenanceEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => {
          patchState(store, { isLoading: false });
          toaster.error('::FailedToLoad');
          return EMPTY;
        }),
      )
    ),
    create: rxMethod<CreateUpdateAssetMaintenanceDto>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as AssetMaintenanceEntity, { selectId: (e) => e.id! }));
          toaster.success('::SuccessfullyCreated');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Create failed');
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
