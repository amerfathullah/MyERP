import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import type { WorkOrderDto } from '../../proxy/manufacturing/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type WOEntity = WorkOrderDto & { id: string };

export const WorkOrderStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<WOEntity>(),
  withMethods((store, service = inject(ManufacturingService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getWorkOrderList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as WOEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.createWorkOrder(input)),
        tap((created) => { patchState(store, addEntity(created as WOEntity, { selectId: (e) => e.id! })); toaster.success('Work Order created'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Create failed'); return EMPTY; }),
      )
    ),
  })),
);
