import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { StatusBadgeComponent, DocumentStatus } from '../status-badge/status-badge.component';

export interface WorkflowAction {
  name: string;
  label: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-document-workflow',
  standalone: true,
  imports: [CommonModule, LocalizationPipe, StatusBadgeComponent],
  templateUrl: './document-workflow.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./document-workflow.component.scss'],
})
export class DocumentWorkflowComponent {
  @Input() currentStatus: string = '';
  @Input() actions: WorkflowAction[] = [];
  /** @deprecated Use actions instead */
  @Input() set availableActions(v: WorkflowAction[]) { this.actions = v; }
  @Output() actionClicked = new EventEmitter<string>();
  /** @deprecated Use actionClicked instead */
  @Output() actionTriggered = this.actionClicked;

  buttonClass(color: string): string {
    return color.startsWith('btn-') ? color : `btn-outline-${color}`;
  }

  iconClass(icon: string): string {
    if (icon.startsWith('fa ')) return icon;
    if (icon.startsWith('fa-')) return `fa ${icon}`;
    return `fa fa-${icon}`;
  }
}
