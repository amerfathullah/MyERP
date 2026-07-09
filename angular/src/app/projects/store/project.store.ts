import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, addEntity, updateEntity, removeEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ProjectService } from '../../proxy/projects/project.service';
import type { ProjectDto } from '../../proxy/projects/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type ProjectEntity = ProjectDto & { id: string };

export const ProjectStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
    filter: '' as string,
  }),
  withEntities<ProjectEntity>(),
  withComputed((store) => ({
    hasProjects: computed(() => store.ids().length > 0),
  })),
  withMethods((store, service = inject(ProjectService), toaster = inject(ToasterService)) => ({

    load: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getList(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as ProjectEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error('Failed to load projects');
          return EMPTY;
        }),
      )
    ),

    create: rxMethod<any>(
      pipe(
        switchMap((input) => service.create(input)),
        tap((created) => {
          patchState(store, addEntity(created as ProjectEntity, { selectId: (e) => e.id! }));
          toaster.success('Project created');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Create failed');
          return EMPTY;
        }),
      )
    ),

    update: rxMethod<{ id: string; input: any }>(
      pipe(
        switchMap(({ id, input }) => service.update(id, input)),
        tap((updated) => {
          patchState(store, updateEntity({ id: updated.id!, changes: updated as ProjectEntity }));
          toaster.success('Project updated');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Update failed');
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
            toaster.success('Project deleted');
          }),
        )),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Delete failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
