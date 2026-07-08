import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaymentEntryService } from '../../proxy/accounting/payment-entry.service';
import type { PaymentEntryDto, CreatePaymentEntryDto } from '../../proxy/accounting/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type PaymentEntryEntity = PaymentEntryDto & { id: EntityId };

export interface PaymentEntryFilter {
  paymentType?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const PaymentEntryStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as PaymentEntryFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<PaymentEntryEntity>(),
  withComputed((store) => ({
    selectedPayment: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasPayments: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(PaymentEntryService), toaster = inject(ToasterService)) => ({
    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as PaymentEntryEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load payments');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreatePaymentEntryDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as PaymentEntryEntity));
          patchState(store, { isLoading: false });
          toaster.success('Payment entry created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitPayment: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as PaymentEntryEntity }));
          toaster.success('Payment submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: PaymentEntryFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
