import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CompanyService } from '../../proxy/core/company.service';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import {
  WorkOrderStatusReportComponent,
  buildWorkOrderStatusCsvRows,
} from './work-order-status-report.component';

describe('WorkOrderStatusReportComponent', () => {
  it('formats CSV rows cleanly', () => {
    const csv = buildWorkOrderStatusCsvRows([
      {
        workOrderId: 'wo-1',
        workOrderNumber: 'WO-0001',
        creationTime: '2026-06-01T00:00:00Z',
        itemCode: 'FG-01',
        itemName: 'Finished Good',
        quantity: 20,
        producedQuantity: 15,
        processLossQty: 2,
        statusLabel: 'In Process',
        stockEntryCount: 3,
      },
    ]);

    expect(csv).toHaveLength(1);
    expect(csv[0]['Work Order']).toBe('WO-0001');
    expect(csv[0]['To Produce']).toBe(20);
    expect(csv[0]['Produced']).toBe(15);
    expect(csv[0]['Stock Entries']).toBe(3);
  });

  it('calls open, in-progress, and completed work orders report endpoints', () => {
    const mfgMock = {
      getOpenWorkOrdersReport: vi.fn().mockReturnValue(of([{ workOrderId: 'wo-open', quantity: 10, producedQuantity: 0 }])),
      getWorkOrdersInProgressReport: vi.fn().mockReturnValue(of([{ workOrderId: 'wo-ip', quantity: 10, producedQuantity: 4 }])),
      getCompletedWorkOrdersReport: vi.fn().mockReturnValue(of([{ workOrderId: 'wo-done', quantity: 10, producedQuantity: 10 }])),
    };

    const injector = Injector.create({
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { data: { reportMode: 'open' } } } },
        { provide: ManufacturingService, useValue: mfgMock },
        { provide: CompanyService, useValue: { getList: () => of({ items: [{ id: 'co-1' }] }) } },
        { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
        { provide: ToasterService, useValue: { error: vi.fn() } },
      ],
    });

    const comp = runInInjectionContext(injector, () => new WorkOrderStatusReportComponent());
    comp.ngOnInit();
    expect(mfgMock.getOpenWorkOrdersReport).toHaveBeenCalledWith('co-1');
    expect(comp.totalToProduce()).toBe(10);

    comp.switchMode('in-progress');
    expect(mfgMock.getWorkOrdersInProgressReport).toHaveBeenCalledWith('co-1');

    comp.switchMode('completed');
    expect(mfgMock.getCompletedWorkOrdersReport).toHaveBeenCalledWith('co-1');
    expect(comp.totalProduced()).toBe(10);
  });
});
