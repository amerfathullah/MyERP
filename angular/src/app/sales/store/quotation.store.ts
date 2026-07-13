import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, type EntityId } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { QuotationService } from '../../proxy/sales/quotation.service';
import type { QuotationDto, CreateQuotationDto } from '../../proxy/sales/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type QuotationEntity = QuotationDto & { id: EntityId };

export interface QuotationFilter {
  status?: string;
  customerName?: string;
  dateFrom?: string;
  dateTo?: string;
}

export const QuotationStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as QuotationFilter,
    totalCount: 0,
    isLoading: false,
    selectedId: null as string | null,
  }),
  withEntities<QuotationEntity>(),
  withComputed((store) => ({
    selectedQuotation: computed(() => store.entityMap()[store.selectedId() ?? '']),
    hasQuotations: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(QuotationService), toaster = inject(ToasterService)) => ({
    load: rxMethod<any>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as QuotationEntity[]));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load quotations');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<CreateQuotationDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as QuotationEntity));
          patchState(store, { isLoading: false });
          toaster.success('Quotation created');
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    submitQuotation: rxMethod<string>(
      pipe(
        switchMap((id) => service.submit(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as QuotationEntity }));
          toaster.success('Quotation submitted');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Submit failed');
          return EMPTY;
        }),
      )
    ),

    cancelQuotation: rxMethod<string>(
      pipe(
        switchMap((id) => service.cancel(id)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as QuotationEntity }));
          toaster.success('Quotation cancelled');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Cancel failed');
          return EMPTY;
        }),
      )
    ),

    setFilter(filter: QuotationFilter) {
      patchState(store, { filter });
    },
    select(id: string | null) {
      patchState(store, { selectedId: id });
    },
  })),
);
