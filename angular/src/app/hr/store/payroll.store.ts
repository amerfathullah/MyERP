import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { PayrollService } from '../../proxy/hr/payroll.service';
import type { PayrollEntryDto, CreatePayrollEntryDto } from '../../proxy/hr/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type PayrollEntity = PayrollEntryDto & { id: EntityId };

export const PayrollStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
  }),
  withEntities<PayrollEntity>(),
  withComputed((store) => ({
    hasEntries: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(PayrollService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as PayrollEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load payroll entries');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreatePayrollEntryDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as PayrollEntity));
          patchState(store, { isLoading: false });
          toaster.success('Payroll created and calculated');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitEntry: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id! as EntityId, changes: updated as PayrollEntity }));
          toaster.success('Payroll submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    cancelEntry: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id! as EntityId, changes: updated as PayrollEntity }));
          toaster.success('Payroll cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
