import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetService } from '../../proxy/assets/asset.service';
import type { AssetDto, GetAssetListDto } from '../../proxy/assets/models';

type AssetEntity = AssetDto & { id: string };

export const AssetStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<AssetEntity>(),
  withMethods((store, service = inject(AssetService), toaster = inject(ToasterService)) => ({
    load: rxMethod<GetAssetListDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as AssetEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load assets'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => { patchState(store, addEntity(created as AssetEntity, { selectId: (e) => e.id! })); toaster.success('Asset created'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Create failed'); return EMPTY; }),
      )
    ),
    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.delete(id).pipe(tap(() => { patchState(store, removeEntity(id)); toaster.success('Asset deleted'); }))),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Delete failed'); return EMPTY; }),
      )
    ),
  })),
);
