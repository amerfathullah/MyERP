import { Component, ChangeDetectionStrategy } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { HierarchyTreeComponent } from '../../shared/components/hierarchy-tree/hierarchy-tree.component';

@Component({
  selector: 'app-territory-tree',
  standalone: true,
  imports: [HierarchyTreeComponent, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `<app-hierarchy-tree type="Territory" [title]="'::Territories' | abpLocalization" iconClass="bi bi-globe" />`,
})
export class TerritoryTreeComponent {}
