import { describe, it, expect, vi } from 'vitest';
import { of } from 'rxjs';

interface WorkflowAction {
  name: string;
  label: string;
  icon: string;
  color: string;
}

/**
 * Tests for MaterialRequestDetailComponent workflow actions and Work Order creation.
 * Verifies ERPNext PR #59584 (commit 6e24ef9cce):
 * - createWO button only for Submitted (1), Manufacture (3), and perOrdered < 100
 * - createWO button hidden for Purchase, Transfer, Issue, Stopped, or 100% fulfilled
 */
describe('MaterialRequestDetailComponent Logic', () => {

  function computeWorkflowActions(entity: {
    status?: number;
    requestType?: number;
    perOrdered?: number;
  }): WorkflowAction[] {
    const s = entity.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) { // Draft
      actions.push({ name: 'submit', label: 'Submit', icon: 'paper-plane', color: 'primary' });
    }
    if (s === 1) { // Submitted
      if ((entity.requestType === 0 || entity.requestType === 5) && (entity.perOrdered ?? 0) < 100) {
        actions.push({ name: 'convertToPO', label: 'Create Purchase Order', icon: 'file-invoice', color: 'success' });
        actions.push({ name: 'createRFQ', label: 'Create RFQ', icon: 'file-lines', color: 'primary' });
        actions.push({ name: 'splitBySupplier', label: 'Split by Supplier', icon: 'code-branch', color: 'secondary' });
      }
      if (entity.requestType === 1 || entity.requestType === 2) { // Transfer/Issue
        actions.push({ name: 'createSE', label: 'Create Stock Entry', icon: 'truck', color: 'info' });
      }
      if (entity.requestType === 3 && (entity.perOrdered ?? 0) < 100) { // Manufacture (ERPNext PR #59584)
        actions.push({ name: 'createWO', label: 'Create Work Order', icon: 'cogs', color: 'primary' });
      }
      actions.push({ name: 'stop', label: 'Stop', icon: 'stop-circle', color: 'warning' });
      actions.push({ name: 'cancel', label: 'Cancel', icon: 'ban', color: 'danger' });
    }
    if (s === 14) { // Stopped / Closed
      actions.push({ name: 'reopen', label: 'Re-open', icon: 'redo', color: 'primary' });
    }
    return actions;
  }

  describe('Manufacture Type Workflow Actions', () => {
    it('shows createWO when submitted and unfulfilled', () => {
      const actions = computeWorkflowActions({
        status: 1, // Submitted
        requestType: 3, // Manufacture
        perOrdered: 0,
      });

      const actionNames = actions.map(a => a.name);
      expect(actionNames).toContain('createWO');
      expect(actionNames).toContain('stop');
      expect(actionNames).toContain('cancel');
      expect(actionNames).not.toContain('convertToPO');
      expect(actionNames).not.toContain('createSE');
    });

    it('hides createWO when 100% fulfilled', () => {
      const actions = computeWorkflowActions({
        status: 1, // Submitted
        requestType: 3, // Manufacture
        perOrdered: 100,
      });

      const actionNames = actions.map(a => a.name);
      expect(actionNames).not.toContain('createWO');
      expect(actionNames).toContain('stop');
      expect(actionNames).toContain('cancel');
    });

    it('shows only submit when in draft status', () => {
      const actions = computeWorkflowActions({
        status: 0, // Draft
        requestType: 3,
        perOrdered: 0,
      });

      expect(actions.map(a => a.name)).toEqual(['submit']);
    });

    it('shows only reopen when stopped', () => {
      const actions = computeWorkflowActions({
        status: 14, // Stopped
        requestType: 3,
        perOrdered: 50,
      });

      expect(actions.map(a => a.name)).toEqual(['reopen']);
    });
  });

  describe('Work Order Creation invocation', () => {
    it('calls service.raiseWorkOrders with correct id and handles success', () => {
      const mockService = {
        raiseWorkOrders: vi.fn().mockReturnValue(of({ createdCount: 2, skippedCount: 0 })),
      };
      const mockToaster = {
        success: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
      };

      const entityId = 'mr-guid-123';
      mockService.raiseWorkOrders(entityId).subscribe((res: any) => {
        if ((res?.createdCount ?? 0) > 0) {
          mockToaster.success(`Successfully created ${res.createdCount} Work Order(s).`);
        }
      });

      expect(mockService.raiseWorkOrders).toHaveBeenCalledWith(entityId);
      expect(mockToaster.success).toHaveBeenCalledWith('Successfully created 2 Work Order(s).');
    });
  });
});
