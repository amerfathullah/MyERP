import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { EmployeeDto, CreateUpdateEmployeeDto } from '../../proxy/human-resources/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type EmployeeEntity = EmployeeDto & { id: EntityId };

export const EmployeeStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
  }),
  withEntities<EmployeeEntity>(),
  withComputed((store) => ({
    hasEmployees: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(EmployeeService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as EmployeeEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load employees');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateUpdateEmployeeDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as EmployeeEntity));
          patchState(store, { isLoading: false });
          toaster.success('Employee created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    remove: rxMethod<string>(
      pipe(
        switchMap((id) => service.delete(id).pipe(tap(() => {
          patchState(store, removeEntity(id as EntityId));
          patchState(store, { totalCount: store.totalCount() - 1 });
          toaster.success('Employee deleted');
        }))),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
