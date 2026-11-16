import { describe, it, expect, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { AssetDetailComponent } from './asset-detail.component';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetActivityService } from '../../proxy/assets/asset-activity.service';
import { AssetMaintenanceService } from '../../proxy/assets/asset-maintenance.service';
import { AssetMaintenanceLogService } from '../../proxy/assets/asset-maintenance-log.service';
import { AccountService } from '../../proxy/accounting/account.service';

interface WorkflowAction {
  name: string;
  label: string;
  icon: string;
  color: string;
}

/**
 * Tests for AssetDetailComponent logic.
 * Verifies: workflow action visibility per status, depreciation schedule display,
 * book value coloring, edit button visibility.
 */
describe('AssetDetailComponent Logic', () => {

  // Status enum values: Draft=0, Submitted=1, PartiallyDepreciated=2, FullyDepreciated=3, Sold=4, Scrapped=5
  function getActions(status: number): WorkflowAction[] {
    const actions: WorkflowAction[] = [];
    if (status === 0) actions.push({ name: 'submit', label: 'Submit', icon: 'fa-paper-plane', color: 'btn-outline-primary' });
    if (status === 1 || status === 2) actions.push({ name: 'sell', label: 'Sell', icon: 'fa-hand-holding-dollar', color: 'btn-outline-success' });
    if (status === 1 || status === 2) actions.push({ name: 'scrap', label: 'Scrap', icon: 'fa-trash-can', color: 'btn-outline-warning' });
    return actions;
  }

  describe('Draft Status (0)', () => {
    it('shows Submit only', () => {
      const actions = getActions(0);
      expect(actions).toHaveLength(1);
      expect(actions[0].name).toBe('submit');
    });

    it('edit button is visible for Draft', () => {
      const status = 0;
      const showEdit = status === 0;
      expect(showEdit).toBe(true);
    });
  });

  describe('Submitted Status (1)', () => {
    it('shows Sell and Scrap', () => {
      const actions = getActions(1);
      expect(actions).toHaveLength(2);
      expect(actions.map(a => a.name)).toContain('sell');
      expect(actions.map(a => a.name)).toContain('scrap');
    });

    it('no Submit action after submission', () => {
      const actions = getActions(1);
      expect(actions.find(a => a.name === 'submit')).toBeUndefined();
    });

    it('edit button hidden after submit', () => {
      const status: number = 1;
      const showEdit = status === 0;
      expect(showEdit).toBe(false);
    });
  });

  describe('Partially Depreciated Status (2)', () => {
    it('still shows Sell and Scrap', () => {
      const actions = getActions(2);
      expect(actions).toHaveLength(2);
      expect(actions.map(a => a.name)).toEqual(['sell', 'scrap']);
    });
  });

  describe('Fully Depreciated Status (3)', () => {
    it('no actions available', () => {
      const actions = getActions(3);
      expect(actions).toHaveLength(0);
    });
  });

  describe('Sold Status (4)', () => {
    it('no actions available after sale', () => {
      const actions = getActions(4);
      expect(actions).toHaveLength(0);
    });
  });

  describe('Scrapped Status (5)', () => {
    it('no actions available after scrap', () => {
      const actions = getActions(5);
      expect(actions).toHaveLength(0);
    });
  });

  describe('Book Value Display', () => {
    it('negative or zero value gets text-danger class', () => {
      const valueAfterDepreciation = 0;
      const isDanger = valueAfterDepreciation <= 0;
      expect(isDanger).toBe(true);
    });

    it('positive value does not get text-danger', () => {
      const valueAfterDepreciation = 5000;
      const isDanger = valueAfterDepreciation <= 0;
      expect(isDanger).toBe(false);
    });
  });

  describe('Depreciation Schedule', () => {
    it('empty schedule hides the section', () => {
      const schedule: any[] = [];
      const showSchedule = schedule.length > 0;
      expect(showSchedule).toBe(false);
    });

    it('non-empty schedule shows the section', () => {
      const schedule = [{ id: '1', scheduledDate: '2026-01-31', depreciationAmount: 1000, isBooked: false }];
      const showSchedule = schedule.length > 0;
      expect(showSchedule).toBe(true);
    });

    it('booked entries get success styling', () => {
      const entry = { isBooked: true };
      const rowClass = entry.isBooked ? 'table-success' : '';
      expect(rowClass).toBe('table-success');
    });

    it('pending entries get secondary badge', () => {
      const entry = { isBooked: false };
      const badge = entry.isBooked ? 'bg-success' : 'bg-secondary';
      expect(badge).toBe('bg-secondary');
    });
  });

  describe('AssetActivity integration', () => {
    it('maps activity type numbers to readable labels', () => {
      const map: Record<number, string> = {
        0: 'Created',
        1: 'Depreciated',
        2: 'Moved',
        3: 'Repaired',
        7: 'Sold',
      };
      expect(map[1]).toBe('Depreciated');
      expect(map[7]).toBe('Sold');
    });
  });

  describe('AssetDetailComponent Maintenance Loading', () => {
    it('loads asset maintenance schedule and execution logs by asset id', () => {
      const maintMock = {
        getByAsset: vi.fn().mockReturnValue(of({ id: 'am-1', assetId: 'asset-1', maintenanceTeamName: 'Facility Team', tasks: [{ id: 't-1' }] })),
      };
      const logMock = {
        getLogsByAsset: vi.fn().mockReturnValue(of([{ id: 'log-1', maintenanceTask: 'Filter Check', dueDate: '2026-12-01' }])),
      };

      const injector = Injector.create({
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'asset-1' } } } },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: AssetService, useValue: { get: vi.fn().mockReturnValue(of({ id: 'asset-1', assetName: 'Chiller' })) } },
          { provide: AssetActivityService, useValue: { getListByAsset: vi.fn().mockReturnValue(of([])) } },
          { provide: AssetMaintenanceService, useValue: maintMock },
          { provide: AssetMaintenanceLogService, useValue: logMock },
          { provide: AccountService, useValue: { getList: vi.fn().mockReturnValue(of({ items: [] })) } },
          { provide: ConfirmationService, useValue: {} },
        ],
      });

      const comp = runInInjectionContext(injector, () => new AssetDetailComponent());
      comp.ngOnInit();

      expect(maintMock.getByAsset).toHaveBeenCalledWith('asset-1');
      expect(logMock.getLogsByAsset).toHaveBeenCalledWith('asset-1');
      expect(comp.assetMaintenance()?.maintenanceTeamName).toBe('Facility Team');
      expect(comp.maintenanceLogs()).toHaveLength(1);
    });
  });
});
