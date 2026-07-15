import { signalStore, withState, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { IssueService } from '../../proxy/support/issue.service';
import type { IssueDto, GetIssueListDto } from '../../proxy/support/models';

type IssueEntity = IssueDto & { id: string };

export const IssueStore = signalStore(
  { providedIn: 'root' },
  withState({ totalCount: 0, isLoading: false }),
  withEntities<IssueEntity>(),
  withMethods((store, service = inject(IssueService), toaster = inject(ToasterService)) => ({
    load: rxMethod<GetIssueListDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as IssueEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError(() => { patchState(store, { isLoading: false }); toaster.error('Failed to load'); return EMPTY; }),
      )
    ),
    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => { patchState(store, addEntity(created as IssueEntity, { selectId: (e) => e.id! })); toaster.success('Issue created'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Create failed'); return EMPTY; }),
      )
    ),
    resolve: rxMethod<{ id: string; resolution?: string }>(
      pipe(
        switchMap(({ id, resolution }) => service.resolve(id, resolution)),
        tap((updated) => { patchState(store, updateEntity({ id: updated.id!, changes: updated as IssueEntity })); toaster.success('Issue resolved'); }),
        catchError((err) => { toaster.error(err?.error?.error?.message ?? 'Failed'); return EMPTY; }),
      )
    ),
  })),
);
