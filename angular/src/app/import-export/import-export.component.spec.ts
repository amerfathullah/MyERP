import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { ImportExportComponent } from './import-export.component';
import { ImportExportService } from '../proxy/import-export/import-export.service';

describe('ImportExportComponent Status Polling', () => {
  it('refreshes single import job row via ImportExportService.getImportStatus', () => {
    const ieMock = {
      getImportHistory: vi.fn().mockReturnValue(of({
        items: [{ id: 'job-1', fileName: 'customers.csv', entityType: 'Customer', status: 1, totalRows: 50, successCount: 10, failureCount: 0 }],
      })),
      getImportStatus: vi.fn().mockReturnValue(of({
        id: 'job-1', fileName: 'customers.csv', entityType: 'Customer', status: 2, totalRows: 50, successCount: 50, failureCount: 0,
      })),
    };

    const injector = Injector.create({
      providers: [
        { provide: FormBuilder, useValue: new FormBuilder() },
        { provide: ImportExportService, useValue: ieMock },
        { provide: ToasterService, useValue: { success: vi.fn(), warn: vi.fn(), error: vi.fn() } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new ImportExportComponent());
    comp.ngOnInit();
    expect(comp.importHistory()[0].status).toBe(1);

    comp.refreshJobStatus('job-1');
    expect(ieMock.getImportStatus).toHaveBeenCalledWith('job-1');
    expect(comp.importHistory()[0].status).toBe(2);
    expect(comp.importHistory()[0].successCount).toBe(50);
    expect(comp.refreshingJobId()).toBeNull();
  });
});
