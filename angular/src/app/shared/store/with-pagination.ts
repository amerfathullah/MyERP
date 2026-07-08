import { computed } from '@angular/core';
import { signalStoreFeature, withState, withComputed, withMethods, patchState } from '@ngrx/signals';

export function withPagination(defaults?: { pageSize?: number }) {
  const pageSize = defaults?.pageSize ?? 10;

  return signalStoreFeature(
    withState({ currentPage: 0, pageSize, totalCount: 0 }),
    withComputed(({ totalCount, pageSize: ps }) => ({
      totalPages: computed(() => Math.ceil(totalCount() / ps())),
      hasNextPage: computed(() => (totalCount() / ps()) > 1),
    })),
    withMethods((store) => ({
      setPage(page: number) {
        patchState(store, { currentPage: page });
      },
      setPageSize(size: number) {
        patchState(store, { pageSize: size, currentPage: 0 });
      },
      setTotalCount(count: number) {
        patchState(store, { totalCount: count });
      },
    })),
  );
}
