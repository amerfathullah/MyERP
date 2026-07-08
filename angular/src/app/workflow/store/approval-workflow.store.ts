import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { withEntities, setAllEntities, updateEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { computed, inject } from '@angular/core';
import { pipe, switchMap, tap, catchError, EMPTY } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ApprovalWorkflowService } from '../../proxy/workflow/approval-workflow.service';
import type { ApprovalRuleDto, ApprovalRequestDto } from '../../proxy/workflow/models';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

type ApprovalRuleEntity = ApprovalRuleDto & { id: string };
type ApprovalRequestEntity = ApprovalRequestDto & { id: string };

export const ApprovalWorkflowStore = signalStore(
  { providedIn: 'root' },
  withState({
    totalCount: 0,
    isLoading: false,
    pendingApprovals: [] as ApprovalRequestDto[],
    pendingCount: 0,
  }),
  withEntities<ApprovalRuleEntity>(),
  withComputed((store) => ({
    activeRules: computed(() => store.entities().filter(r => r.isActive)),
  })),
  withMethods((store, service = inject(ApprovalWorkflowService), toaster = inject(ToasterService)) => ({
    loadRules: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getRules(query)),
        tap((result) => {
          patchState(store, setAllEntities((result.items ?? []) as ApprovalRuleEntity[], { selectId: (e) => e.id! }));
          patchState(store, { totalCount: result.totalCount ?? 0, isLoading: false });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error(err?.error?.error?.message ?? 'Failed to load approval rules');
          return EMPTY;
        }),
      )
    ),

    loadPendingApprovals: rxMethod<PagedAndSortedResultRequestDto>(
      pipe(
        tap(() => patchState(store, { isLoading: true })),
        switchMap((query) => service.getPendingApprovals(query)),
        tap((result) => {
          patchState(store, {
            pendingApprovals: result.items ?? [],
            pendingCount: result.totalCount ?? 0,
            isLoading: false,
          });
        }),
        catchError((err) => {
          patchState(store, { isLoading: false });
          toaster.error('Failed to load pending approvals');
          return EMPTY;
        }),
      )
    ),

    approve: rxMethod<{ requestId: string; remarks?: string }>(
      pipe(
        switchMap(({ requestId, remarks }) => service.approve({ requestId, remarks })),
        tap((result) => {
          patchState(store, {
            pendingApprovals: store.pendingApprovals().filter(a => a.id !== result.id),
            pendingCount: store.pendingCount() - 1,
          });
          toaster.success('Approved successfully');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Approve failed');
          return EMPTY;
        }),
      )
    ),

    reject: rxMethod<{ requestId: string; remarks?: string }>(
      pipe(
        switchMap(({ requestId, remarks }) => service.reject({ requestId, remarks })),
        tap((result) => {
          patchState(store, {
            pendingApprovals: store.pendingApprovals().filter(a => a.id !== result.id),
            pendingCount: store.pendingCount() - 1,
          });
          toaster.success('Rejected');
        }),
        catchError((err) => {
          toaster.error(err?.error?.error?.message ?? 'Reject failed');
          return EMPTY;
        }),
      )
    ),
  })),
);
