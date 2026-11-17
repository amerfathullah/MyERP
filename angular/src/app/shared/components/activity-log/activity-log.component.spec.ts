import { describe, it, expect, vi } from 'vitest';
import { of } from 'rxjs';
import { ActivityLogComponent } from './activity-log.component';

describe('ActivityLogComponent', () => {
  it('should skip activity fetch on unsaved/new document (ERPNext PR #59386)', () => {
    const mockService = {
      getForDocument: vi.fn().mockReturnValue(of([])),
    };

    const component = new ActivityLogComponent();
    (component as any).activityLogService = mockService;
    component.documentType = 'Lead';
    component.documentId = 'new';

    component.ngOnInit();
    expect(mockService.getForDocument).not.toHaveBeenCalled();
    expect(component.logs).toEqual([]);

    component.loadLogs();
    expect(mockService.getForDocument).not.toHaveBeenCalled();
  });

  it('should fetch activities for saved document with valid id', () => {
    const mockLogs = [
      { id: '1', activityType: 'Submitted', creationTime: '2026-09-24T12:00:00Z', note: 'Created' },
    ];
    const mockService = {
      getForDocument: vi.fn().mockReturnValue(of(mockLogs)),
    };

    const component = new ActivityLogComponent();
    (component as any).activityLogService = mockService;
    component.documentType = 'Lead';
    component.documentId = 'lead-123';

    component.ngOnInit();
    expect(mockService.getForDocument).toHaveBeenCalledWith('Lead', 'lead-123');
    expect(component.logs).toEqual(mockLogs);
  });

  it('keeps paragraphs and line breaks apart and handles empty/null descriptions (ERPNext commit 135403ad9d)', () => {
    const component = new ActivityLogComponent();
    expect(component.formatDescription(null)).toBe('');
    expect(component.formatDescription(undefined)).toBe('');
    expect(component.formatDescription('null')).toBe('');
    expect(component.formatDescription('<p>First paragraph</p><p>Second paragraph</p>')).toBe(
      'First paragraph Second paragraph'
    );
    expect(component.formatDescription('Line one<br/>Line two')).toBe('Line one Line two');
  });
});
