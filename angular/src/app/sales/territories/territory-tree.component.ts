import { Component, ChangeDetectionStrategy } from '@angular/core';
import { HierarchyTreeComponent } from '../../shared/components/hierarchy-tree/hierarchy-tree.component';

@Component({
  selector: 'app-territory-tree',
  standalone: true,
  imports: [HierarchyTreeComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-hierarchy-tree type="Territory" title="Territories" iconClass="bi bi-globe" />`,
})
export class TerritoryTreeComponent {}
