import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { CustomerService } from '../../proxy/sales/customer.service';
import type { CustomerDto, CreateUpdateCustomerDto, GetCustomerListDto } from '../../proxy/sales/models';

type CustomerEntity = CustomerDto & { id: EntityId };

export interface CustomerFilter {
  search?: string;
  isActive?: boolean;
}

export const CustomerStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as CustomerFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<CustomerEntity>(),
  withComputed((store) => ({
    selectedCustomer: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasCustomers: computed(() => store.ids().length > 0),
  })),
  withMethods((store, customerService = inject(CustomerService), toaster = inject(ToasterService)) => ({
    load: rxMethod<GetCustomerListDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => customerService.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as CustomerEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load customers');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateUpdateCustomerDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => customerService.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as CustomerEntity));
          patchState(store, { isLoading: false });
          toaster.success('Customer created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    update: rxMethod<{ id: string; input: CreateUpdateCustomerDto }>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap(({ id, input }) => customerService.update(id, input)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as CustomerEntity }));
          patchState(store, { isLoading: false });
          toaster.success('Customer updated');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Update failed');
          return EMPTY;
        }),
      )
    ),

    remove: rxMethod<string>(
      pipe(
        switchMap((id) => customerService.delete(id).pipe(
          tap(() => {
            patchState(store, removeEntity(id));
            patchState(store, { totalCount: store.totalCount() - 1 });
            toaster.success('Customer deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: CustomerFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
