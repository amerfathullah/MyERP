import { signalStoreFeature, withState, withMethods, patchState } from '@ngrx/signals';

export function withLoading() {
  return signalStoreFeature(
    withState({ isLoading: false }),
    withMethods((store) => ({
      setLoading(loading: boolean) {
        patchState(store, { isLoading: loading });
      },
    })),
  );
}
