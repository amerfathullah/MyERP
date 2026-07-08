import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities } from '@ngrx/signals/entities';
import { computed } from '@angular/core';

export interface ItemListItem {
  id: string;
  itemCode: string;
  itemName: string;
  itemGroup: string;
  uom: string;
  stockQty: number;
  rate: number;
  isActive: boolean;
}

export interface ItemFilter {
  itemGroup?: string;
  search?: string;
}

export const ItemStore = signalStore(
  { providedIn: 'root' },
  withState({
    filter: {} as ItemFilter,
    totalCount: 0,
    isLoading: false,
  }),
  withEntities<ItemListItem>(),
  withComputed((store) => ({
    hasItems: computed(() => store.ids().length > 0),
  })),
  withMethods((store) => ({
    setLoading(isLoading: boolean) {
      patchState(store, { isLoading });
    },
    setFilter(filter: ItemFilter) {
      patchState(store, { filter });
    },
    loadSuccess(items: ItemListItem[], totalCount: number) {
      patchState(store, setAllEntities(items));
      patchState(store, { totalCount, isLoading: false });
    },
  })),
);
