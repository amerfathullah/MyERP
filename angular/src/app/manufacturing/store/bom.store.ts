import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { BomDto } from '../../proxy/manufacturing/models';

type BomEntity = BomDto & { id: string };

export const BomStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<BomEntity>(),
  withMethods((store, service = inject(ManufacturingService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getBomList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as BomEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load BOMs'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.createBom(input)),
        tap((created) => {
          patchState(store, addEntity(created as BomEntity, { selectId: (e) => e.id! }));
          toaster.success('BOM created');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Create failed'); return EMPTY; }),
      )
    ),
    update: rxMethod<{ id: string; input: any }>(
      pipe(
        switchMap(({ id, input }) => service.updateBom(id, input)),
        tap((updated) => {
          patchState(store, updateEntity({ id: (updated as BomEntity).id!, changes: updated as BomEntity }));
          toaster.success('BOM updated');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Update failed'); return EMPTY; }),
      )
    ),
    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.deleteBom(id).pipe(tap(() => {
          patchState(store, removeEntity(id));
          toaster.success('BOM deleted');
        }))),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Delete failed'); return EMPTY; }),
      )
    ),
  })),
);
