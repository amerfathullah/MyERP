import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { AutomationRuleService } from '../../proxy/automation/automation-rule.service';
import type { AutomationRuleDto, CreateAutomationRuleDto, UpdateAutomationRuleDto } from '../../proxy/automation/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type AutomationRuleEntity = AutomationRuleDto & { id: string };

export const AutomationRuleStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
  }),
  withEntities<AutomationRuleEntity>(),
  withMethods((store, service = inject(AutomationRuleService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as AutomationRuleEntity[], { selectId: (e) => e.id }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load automation rules');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateAutomationRuleDto>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as AutomationRuleEntity, { selectId: (e) => e.id }));
          patchState(store, { totalCount: store.totalCount() + 1 });
          toaster.success('Automation rule created');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.delete(id).pipe(
          tap(() => {
            patchState(store, removeEntity(id));
            patchState(store, { totalCount: store.totalCount() - 1 });
            toaster.success('Deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),

    toggleActive: rxMethod<string>(
      pipe(
        switchMap((id) => service.toggleActive(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as AutomationRuleEntity }));
          toaster.success(updated.isActive ? 'Rule activated' : 'Rule deactivated');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Toggle failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
