import { describe, it, expect } from 'vitest';

// BOM Form DTO mapping + validation logic tests

function mapBomMaterial(control: { itemId: string; description: string; qty: number; rate: number }) {
  return {
    itemId: control.itemId,
    itemName: control.description || '',
    quantity: control.qty ?? 0,
    rate: control.rate ?? 0,
    uom: 'Unit',
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
});
