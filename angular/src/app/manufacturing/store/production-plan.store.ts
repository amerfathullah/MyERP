import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProductionPlanService } from '../../proxy/manufacturing/production-plan.service';
import type { ProductionPlanDto, GetProductionPlanListDto } from '../../proxy/manufacturing/models';

type PPEntity = ProductionPlanDto & { id: string };

export const ProductionPlanStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false, selectedPlan: null as ProductionPlanDto | null }),
  withEntities<PPEntity>(),
  withMethods((store, service = inject(ProductionPlanService), toaster = inject(ToasterService)) => ({
    load: rxMethod<GetProductionPlanListDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as PPEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load'); return EMPTY; }),
      )
    ),
    loadOne: rxMethod<string>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((id) => service.get(id)),
        tap((plan) => patchState(store, { selectedPlan: plan, isLoading: false })),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load plan'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => { patchState(store, addEntity(created as PPEntity, { selectId: (e) => e.id! })); toaster.success('Production Plan created'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Create failed'); return EMPTY; }),
      )
    ),
    submit: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PPEntity }));
          patchState(store, { selectedPlan: updated });
          toaster.success('Plan submitted');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Submit failed'); return EMPTY; }),
      )
    ),
    calculateMaterials: rxMethod<string>(
      pipe(
        switchMap((id) => service.calculateMaterialRequirements(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PPEntity }));
          patchState(store, { selectedPlan: updated });
          toaster.success('Materials calculated');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Calculation failed'); return EMPTY; }),
      )
    ),
    generateWorkOrders: rxMethod<string>(
      pipe(
        switchMap((id) => service.generateWorkOrders(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PPEntity }));
          patchState(store, { selectedPlan: updated });
          toaster.success('Work Orders generated');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Generation failed'); return EMPTY; }),
      )
    ),
    generateMaterialRequests: rxMethod<string>(
      pipe(
        switchMap((id) => service.generateMaterialRequests(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PPEntity }));
          patchState(store, { selectedPlan: updated });
          toaster.success('Material Requests generated');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Generation failed'); return EMPTY; }),
      )
    ),
    cancel: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PPEntity }));
          patchState(store, { selectedPlan: updated });
          toaster.success('Plan cancelled');
        }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Cancel failed'); return EMPTY; }),
      )
    ),
  })),
);
