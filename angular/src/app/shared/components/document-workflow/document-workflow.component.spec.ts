import { describe, it, expect } from 'vitest';
import { DocumentWorkflowComponent } from './document-workflow.component';

describe('DocumentWorkflowComponent', () => {
  const comp = new DocumentWorkflowComponent();

  it('normalizes button color classes with or without btn- prefix', () => {
    expect(comp.buttonClass('primary')).toBe('btn-outline-primary');
    expect(comp.buttonClass('danger')).toBe('btn-outline-danger');
    expect(comp.buttonClass('btn-outline-success')).toBe('btn-outline-success');
    expect(comp.buttonClass('btn-warning')).toBe('btn-warning');
  });

  it('normalizes FontAwesome icon classes for bare, fa-, and fa fa- formats', () => {
    expect(comp.iconClass('paper-plane')).toBe('fa fa-paper-plane');
    expect(comp.iconClass('fa-paper-plane')).toBe('fa fa-paper-plane');
    expect(comp.iconClass('fa fa-cloud-arrow-up')).toBe('fa fa-cloud-arrow-up');
  });
});
