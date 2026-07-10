import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import type { MaterialRequestDto } from '../../proxy/purchasing/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type MREntity = MaterialRequestDto & { id: string };

export const MaterialRequestStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<MREntity>(),
  withMethods((store, service = inject(MaterialRequestService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as MREntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as MREntity, { selectId: (e) => e.id! }));
          toaster.success('Material Request created');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Create failed'); return EMPTY; }),
      )
    ),
    submitRequest: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as MREntity }));
          toaster.success('Material Request submitted');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Submit failed'); return EMPTY; }),
      )
    ),
    cancelRequest: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as MREntity }));
          toaster.success('Material Request cancelled');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Cancel failed'); return EMPTY; }),
      )
    ),
    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.delete(id).pipe(
          tap(() => { patchState(store, removeEntity(id)); toaster.success('Deleted'); }),
        )),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Delete failed'); return EMPTY; }),
      )
    ),
  })),
);
