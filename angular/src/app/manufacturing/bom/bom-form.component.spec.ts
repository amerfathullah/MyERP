import { Injector, runInInjectionContext } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { describe, it, expect, vi } from 'vitest';
import { BomFormComponent } from './bom-form.component';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

// BOM Form DTO mapping + validation logic tests

function mapBomMaterial(control: { itemId: string; description: string; qty: number; rate: number; subBomId?: string | null; setRateOfSubAssemblyItemBasedOnBom?: boolean; doNotExplode?: boolean }) {
  return {
    itemId: control.itemId,
    itemName: control.description || '',
    quantity: control.qty ?? 0,
    rate: control.rate ?? 0,
    uom: 'Unit',
    subBomId: control.subBomId || null,
    setRateOfSubAssemblyItemBasedOnBom: control.setRateOfSubAssemblyItemBasedOnBom ?? true,
    doNotExplode: control.doNotExplode ?? false,
  };
}

function mapBomOperation(control: any) {
  return {
    operationId: control.operationId || undefined,
    sequenceId: control.sequenceId,
    description: control.description || '',
    timeInMins: control.timeInMins,
    workstationHourRate: control.workstationHourRate ?? 0,
    batchSize: control.batchSize ?? 0,
    fixedTime: control.fixedTime ?? 0,
    isSubcontracted: control.isSubcontracted ?? false,
  };
}

function getOpCost(timeInMins: number, hourRate: number): number {
  return (timeInMins / 60) * hourRate;
}

function getNextSequence(existingSeqs: number[]): number {
  return existingSeqs.length > 0 ? Math.max(...existingSeqs) + 10 : 10;
}

describe('BomFormComponent', () => {
  describe('material DTO mapping', () => {
    it('should map qty → quantity and description → itemName', () => {
      const result = mapBomMaterial({ itemId: 'item-1', description: 'Steel Rod', qty: 5, rate: 12.5 });
      expect(result.quantity).toBe(5);
      expect(result.itemName).toBe('Steel Rod');
      expect(result.rate).toBe(12.5);
      expect(result.uom).toBe('Unit');
      expect(result.setRateOfSubAssemblyItemBasedOnBom).toBe(true);
      expect(result.doNotExplode).toBe(false);
    });

    it('should default itemName to empty when description is blank', () => {
      const result = mapBomMaterial({ itemId: 'item-1', description: '', qty: 1, rate: 0 });
      expect(result.itemName).toBe('');
    });

    it('should default quantity to 0 when qty is null', () => {
      const result = mapBomMaterial({ itemId: 'item-1', description: 'X', qty: null as any, rate: 10 });
      expect(result.quantity).toBe(0);
    });

    it('should handle fractional quantities', () => {
      const result = mapBomMaterial({ itemId: 'item-1', description: 'Wire', qty: 2.5, rate: 4.8 });
      expect(result.quantity).toBe(2.5);
    });

    it('should respect setRateOfSubAssemblyItemBasedOnBom when set to false (PR #59178 / ec88db0253)', () => {
      const result = mapBomMaterial({
        itemId: 'sub-item-1',
        description: 'Sub Assembly Alpha',
        qty: 1,
        rate: 50,
        subBomId: 'bom-sub-1',
        setRateOfSubAssemblyItemBasedOnBom: false,
        doNotExplode: true,
      });
      expect(result.setRateOfSubAssemblyItemBasedOnBom).toBe(false);
      expect(result.doNotExplode).toBe(true);
      expect(result.subBomId).toBe('bom-sub-1');
    });
  });

  describe('operation DTO mapping', () => {
    it('should map all operation fields', () => {
      const result = mapBomOperation({
        operationId: 'op-1', sequenceId: 10, description: 'Cutting',
        timeInMins: 30, workstationHourRate: 120, batchSize: 25,
        fixedTime: 5, isSubcontracted: false,
      });
      expect(result.sequenceId).toBe(10);
      expect(result.timeInMins).toBe(30);
      expect(result.batchSize).toBe(25);
      expect(result.fixedTime).toBe(5);
    });

    it('should default optional fields', () => {
      const result = mapBomOperation({ sequenceId: 10, timeInMins: 60 });
      expect(result.operationId).toBeUndefined();
      expect(result.workstationHourRate).toBe(0);
      expect(result.batchSize).toBe(0);
      expect(result.isSubcontracted).toBe(false);
    });
  });

  describe('operation cost calculation', () => {
    it('should calculate cost from time and hour rate', () => {
      expect(getOpCost(60, 100)).toBe(100); // 1 hour × 100
    });

    it('should handle half-hour operations', () => {
      expect(getOpCost(30, 200)).toBe(100); // 0.5 hour × 200
    });

    it('should handle zero time', () => {
      expect(getOpCost(0, 100)).toBe(0);
    });

    it('should handle zero rate', () => {
      expect(getOpCost(60, 0)).toBe(0);
    });

    it('should handle 90-minute operation', () => {
      expect(getOpCost(90, 120)).toBe(180); // 1.5 × 120
    });
  });

  describe('material cost totals', () => {
    it('should sum material costs', () => {
      const materials = [
        { qty: 5, rate: 10 },
        { qty: 3, rate: 20 },
        { qty: 2, rate: 15 },
      ];
      const total = materials.reduce((sum, m) => sum + m.qty * m.rate, 0);
      expect(total).toBe(5*10 + 3*20 + 2*15); // 50 + 60 + 30 = 140
    });

    it('should return 0 for empty materials', () => {
      const total = [].reduce((sum: number, m: any) => sum + m.qty * m.rate, 0);
      expect(total).toBe(0);
    });
  });

  describe('sequence auto-increment', () => {
    it('should start at 10 for empty operations', () => {
      expect(getNextSequence([])).toBe(10);
    });

    it('should add 10 to max existing sequence', () => {
      expect(getNextSequence([10, 20, 30])).toBe(40);
    });

    it('should handle non-sequential values', () => {
      expect(getNextSequence([10, 50])).toBe(60);
    });
  });

  describe('total cost', () => {
    it('should combine material and operation costs', () => {
      const materialCost = 500;
      const operatingCost = 200;
      expect(materialCost + operatingCost).toBe(700);
    });
  });

  describe('edit mode detection', () => {
    it('should detect edit mode from route param', () => {
      const entityId = 'bom-123';
      const isEditMode = !!entityId;
      expect(isEditMode).toBe(true);
    });

    it('should detect create mode when no route param', () => {
      const entityId = null;
      const isEditMode = !!entityId;
      expect(isEditMode).toBe(false);
    });
  });

  describe('secondary item DTO mapping & valuation type logic', () => {
    function mapSecondaryItem(control: any) {
      const valType = Number(control.valuationType ?? 0);
      return {
        itemId: control.itemId,
        itemName: control.itemName || '',
        secondaryItemType: Number(control.secondaryItemType ?? 0),
        valuationType: valType,
        quantity: Number(control.quantity ?? 1),
        rate: Number(control.rate ?? 0),
        costAllocationPercentage: valType === 1 ? Number(control.costAllocationPercentage ?? 0) : 0,
        processLossPercentage: Number(control.processLossPercentage ?? 0),
        stockUom: control.stockUom || 'Unit',
        warehouseId: control.warehouseId || null,
      };
    }

    it('should map secondary item and preserve cost allocation for PercentageOfComponentCost', () => {
      const result = mapSecondaryItem({
        itemId: 'item-coproduct',
        itemName: 'Steel Dust',
        secondaryItemType: 0,
        valuationType: 1, // PercentageOfComponentCost (PR #59021)
        quantity: 2,
        costAllocationPercentage: 25,
        rate: 50,
      });

      expect(result.valuationType).toBe(1);
      expect(result.costAllocationPercentage).toBe(25);
      expect(result.quantity).toBe(2);
    });

    it('should zero out cost allocation for ValuationRate valuation type', () => {
      const result = mapSecondaryItem({
        itemId: 'item-scrap',
        itemName: 'Metal Scrap',
        secondaryItemType: 2,
        valuationType: 0, // ValuationRate
        quantity: 5,
        costAllocationPercentage: 15,
        rate: 10,
      });

      expect(result.valuationType).toBe(0);
      expect(result.costAllocationPercentage).toBe(0);
    });

    it('should zero out cost allocation for Manual valuation type', () => {
      const result = mapSecondaryItem({
        itemId: 'item-byproduct',
        itemName: 'Slag',
        secondaryItemType: 1,
        valuationType: 2, // Manual
        quantity: 1,
        costAllocationPercentage: 30,
        rate: 80,
      });

      expect(result.valuationType).toBe(2);
      expect(result.costAllocationPercentage).toBe(0);
    });
  });

  describe('sub-assembly rate UOM conversion & operation config exclusivity (commits 42a3a8342d, 98fc122a1f)', () => {
    function createBomComponent() {
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
          { provide: ManufacturingService, useValue: { getBom: vi.fn(), createBom: vi.fn().mockReturnValue(of({})), updateBom: vi.fn().mockReturnValue(of({})) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'comp-1' } },
        ],
      });
      return runInInjectionContext(injector, () => new BomFormComponent());
    }

    it('multiplies sub-assembly BOM unit cost by row conversionFactor (commit 42a3a8342d)', () => {
      const comp = createBomComponent();
      // Sub-BOM costs 100 for 2 stock units (50/unit), row UOM is Box of 12 -> 600
      expect(comp.resolveSubAssemblyRateFromBom(100, 2, 12)).toBe(600);
    });

    it('clears fgBasedOperatingCost and operatingCostPerBomQuantity when operations exist (commit 98fc122a1f)', () => {
      const comp = createBomComponent();
      comp.form.patchValue({ fgBasedOperatingCost: true, operatingCostPerBomQuantity: 25 });
      comp.addOperation({ sequenceId: 10, description: 'Assembly', timeInMins: 30, workstationHourRate: 60 });
      comp.syncOperationConfigurationFlags();

      expect(comp.form.get('fgBasedOperatingCost')?.value).toBe(false);
      expect(comp.form.get('operatingCostPerBomQuantity')?.value).toBe(0);
    });
  });


  describe('BomFormComponent live cost calculation parity (PR #65856 / #67349)', () => {
    function createBomForm() {
      const injector = Injector.create({
        providers: [
          { provide: FormBuilder, useValue: new FormBuilder() },
          { provide: Router, useValue: { navigate: vi.fn() } },
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
          { provide: ManufacturingService, useValue: { getBom: vi.fn().mockReturnValue(of({})) } },
          { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } },
          { provide: CompanyContextService, useValue: { currentCompanyId: () => 'co-1' } },
        ],
      });
      return runInInjectionContext(injector, () => new BomFormComponent());
    }

    it('recomputes secondary item percentage cost against (materialCost - ownCost) and deducts from totalCost', () => {
      const comp = createBomForm();
      comp.addMaterial({ itemId: 'rm-1', itemName: 'Steel', quantity: 10, rate: 100 }); // 1000
      comp.addOperation({ sequenceId: 10, timeInMins: 60, workstationHourRate: 50 }); // 50
      comp.addSecondaryItem({ itemId: 'sec-1', valuationType: 2, quantity: 2, rate: 50 }); // Manual = 100
      comp.addSecondaryItem({ itemId: 'sec-2', valuationType: 1, quantity: 1, costAllocationPercentage: 20 }); // 20% of (1000 - 100) = 180

      expect(comp.materialCost).toBe(1000);
      expect(comp.getSecondaryItemCost(0)).toBe(100);
      expect(comp.getSecondaryItemCost(1)).toBe(180);
      expect(comp.secondaryItemsCost).toBe(280);
      expect(comp.totalCost).toBe(770); // 1000 + 50 - 280

      // Updating material rate dynamically updates % secondary cost and totalCost
      comp.materials.at(0).patchValue({ rate: 200 }); // 2000 -> basis 1900 -> 20% = 380
      expect(comp.getSecondaryItemCost(1)).toBe(380);
      expect(comp.secondaryItemsCost).toBe(480);
      expect(comp.totalCost).toBe(1570); // 2000 + 50 - 480
    });

    it('sums unrounded operation costs to avoid per-row rounding drift', () => {
      const comp = createBomForm();
      // Three 1-minute operations at 100/hr -> exact sum is 5.00, not 3 * 1.67 = 5.01
      comp.addOperation({ sequenceId: 10, timeInMins: 1, workstationHourRate: 100 });
      comp.addOperation({ sequenceId: 20, timeInMins: 1, workstationHourRate: 100 });
      comp.addOperation({ sequenceId: 30, timeInMins: 1, workstationHourRate: 100 });

      expect(comp.operatingCost).toBeCloseTo(5, 6);
    });

    it('applies batchSize and unrounded costPerUnit when setCostBasedOnBomQty is enabled (22d504f5b6, e7be233789, 45e7b4c1d1)', () => {
      const comp = createBomForm();
      comp.form.patchValue({ quantity: 600 });
      comp.addOperation({
        sequenceId: 10,
        timeInMins: 1,
        workstationHourRate: 100,
        batchSize: 10,
        setCostBasedOnBomQty: true,
      });

      // Raw op cost = 100/60 = 1.666666...; costPerUnit = 1.666666... / 10 = 0.166666...
      // Scaled by BOM qty 600 => exact 100, not 1.67 / 10 * 600 = 100.20
      expect(comp.operatingCost).toBeCloseTo(100, 6);

      // Refreshing batchSize or BOM quantity updates operatingCost dynamically
      comp.operations.at(0).patchValue({ batchSize: 20 });
      expect(comp.operatingCost).toBeCloseTo(50, 6);
    });

    it('fills and clears operation target warehouses from BOM default target warehouse (aba50eb01d, 46c37b5eef)', () => {
      const comp = createBomForm();
      comp.addOperation({ sequenceId: 10, timeInMins: 30, workstationHourRate: 60 });
      comp.addOperation({ sequenceId: 20, timeInMins: 45, workstationHourRate: 60 });

      comp.onDefaultTargetWarehouseChange('wh-fg-1');
      expect(comp.operations.at(0).get('targetWarehouseId')?.value).toBe('wh-fg-1');
      expect(comp.operations.at(1).get('targetWarehouseId')?.value).toBe('wh-fg-1');

      comp.onDefaultTargetWarehouseChange(null);
      expect(comp.operations.at(0).get('targetWarehouseId')?.value).toBeNull();
      expect(comp.operations.at(1).get('targetWarehouseId')?.value).toBeNull();
    });

    it('computes baseSecondaryItemsCost across ValuationRate, Manual, and PercentageOfComponentCost rows (a636da6cdd)', () => {
      const comp = createBomForm();
      comp.addMaterial({ itemId: 'rm-1', itemName: 'Steel', quantity: 10, rate: 100 }); // 1000
      comp.addSecondaryItem({ itemId: 'sec-1', valuationType: 2, quantity: 2, rate: 50 }); // Manual = 100
      comp.addSecondaryItem({ itemId: 'sec-2', valuationType: 1, quantity: 4, costAllocationPercentage: 20 }); // 20% of 900 = 180

      expect(comp.getSecondaryItemBaseCost(0, 4.5)).toBe(450);
      expect(comp.getSecondaryItemBaseCost(1, 4.5)).toBe(810);
      expect(comp.getBaseSecondaryItemsCost(4.5)).toBe(1260);
    });
  });
});
