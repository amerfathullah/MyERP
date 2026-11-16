import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { QualityProcedureListComponent } from './quality-procedure-list.component';
import { QualityManagementService } from '../../proxy/inventory/quality-management.service';

describe('QualityProcedureListComponent', () => {
  let qmServiceMock: any;
  let confirmationMock: any;

  function createComponent(): QualityProcedureListComponent {
    const injector = Injector.create({
      providers: [
        { provide: QualityManagementService, useValue: qmServiceMock },
        { provide: ConfirmationService, useValue: confirmationMock },
      ],
    });
    return runInInjectionContext(injector, () => new QualityProcedureListComponent());
  }

  beforeEach(() => {
    qmServiceMock = {
      getProcedureList: vi.fn().mockReturnValue(of({ items: [{ id: 'p1', name: 'Incoming QC' }], totalCount: 1 })),
      getProcedureTree: vi.fn().mockReturnValue(of([
        { id: 'root', name: 'QA Master', parentQualityProcedureId: null, isGroup: true, steps: [] },
        { id: 'child-1', name: 'Receiving Check', parentQualityProcedureId: 'root', isGroup: false, steps: [{}] },
        { id: 'grandchild-1', name: 'Dimension Check', parentQualityProcedureId: 'child-1', isGroup: false, steps: [] },
        { id: 'orphan', name: 'Standalone SOP', parentQualityProcedureId: 'missing-parent', isGroup: false, steps: [] },
      ])),
      deleteProcedure: vi.fn().mockReturnValue(of(void 0)),
    };
    confirmationMock = { warn: vi.fn().mockReturnValue(of(Confirmation.Status.confirm)) };
  });

  it('loads paginated list on init', () => {
    const comp = createComponent();
    comp.ngOnInit();

    expect(comp.viewMode).toBe('list');
    expect(qmServiceMock.getProcedureList).toHaveBeenCalledTimes(1);
    expect(comp.items).toHaveLength(1);
  });

  it('switches to tree mode via getProcedureTree and preserves parent-child depth ordering', () => {
    const comp = createComponent();
    comp.setViewMode('tree');

    expect(comp.viewMode).toBe('tree');
    expect(qmServiceMock.getProcedureTree).toHaveBeenCalledTimes(1);
    expect(comp.treeNodes.map(n => `${n.depth}:${n.node.id}`)).toEqual([
      '0:root',
      '1:child-1',
      '2:grandchild-1',
      '0:orphan',
    ]);
    expect(comp.isLoading).toBe(false);
  });

  it('filters tree nodes by search term', () => {
    const comp = createComponent();
    comp.setViewMode('tree');
    comp.searchTerm = 'dimension';

    expect(comp.filteredTreeNodes().map(n => n.node.id)).toEqual(['grandchild-1']);
  });

  it('reloads tree after deleting in tree mode', () => {
    const comp = createComponent();
    comp.setViewMode('tree');
    qmServiceMock.getProcedureTree.mockClear();

    comp.deleteProcedure('child-1', 'Receiving Check');

    expect(qmServiceMock.deleteProcedure).toHaveBeenCalledWith('child-1');
    expect(qmServiceMock.getProcedureTree).toHaveBeenCalledTimes(1);
  });

  it('resets loading on tree load error', () => {
    qmServiceMock.getProcedureTree.mockReturnValue(throwError(() => new Error('fail')));
    const comp = createComponent();

    comp.loadTree();

    expect(comp.isLoading).toBe(false);
  });
});
