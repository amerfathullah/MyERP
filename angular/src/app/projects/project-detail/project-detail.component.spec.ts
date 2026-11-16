import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of, throwError } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ProjectDetailComponent } from './project-detail.component';
import { ProjectService } from '../../proxy/projects/project.service';

describe('ProjectDetailComponent Task Dependencies', () => {
  function createComponent(options?: { cycleError?: boolean }) {
    const projectMock = {
      get: vi.fn().mockReturnValue(of({ id: 'proj-1', projectName: 'ERP Launch', status: 0 })),
      getTasks: vi.fn().mockReturnValue(of([
        { id: 't-1', taskNumber: 'TSK-1', subject: 'Schema', status: 0 },
        { id: 't-2', taskNumber: 'TSK-2', subject: 'API', status: 0 },
      ])),
      addTaskDependency: options?.cycleError
        ? vi.fn().mockReturnValue(throwError(() => ({ error: { error: { data: { detail: 'Cyclic dependency detected' } } } })))
        : vi.fn().mockReturnValue(of({ id: 't-2' })),
    };
    const toasterMock = { success: vi.fn(), error: vi.fn() };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'proj-1' } } } },
        { provide: ConfirmationService, useValue: {} },
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ProjectService, useValue: projectMock },
        { provide: ToasterService, useValue: toasterMock },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ProjectDetailComponent());
    comp.ngOnInit();
    return { comp, projectMock, toasterMock };
  }

  it('adds prerequisite dependency via ProjectService and rejects self-dependency', () => {
    const { comp, projectMock, toasterMock } = createComponent();
    const task2 = comp.tasks()[1];

    comp.openDependencyForm(task2);
    comp.dependsOnTaskId.set('t-2');
    comp.addTaskDependency();
    expect(projectMock.addTaskDependency).not.toHaveBeenCalled();

    comp.dependsOnTaskId.set('t-1');
    comp.addTaskDependency();

    expect(projectMock.addTaskDependency).toHaveBeenCalledWith('t-2', 't-1');
    expect(comp.dependencyTask()).toBeNull();
    expect(toasterMock.success).toHaveBeenCalledWith('::DependencyAdded');
  });

  it('surfaces backend cycle validation message cleanly', () => {
    const { comp, toasterMock } = createComponent({ cycleError: true });
    comp.openDependencyForm(comp.tasks()[1]);
    comp.dependsOnTaskId.set('t-1');

    comp.addTaskDependency();

    expect(comp.addingDependency()).toBe(false);
    expect(toasterMock.error).toHaveBeenCalledWith('Cyclic dependency detected');
  });
});
