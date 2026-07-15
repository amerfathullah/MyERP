import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
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
  imports: [CommonModule, StatusBadgeComponent],
  templateUrl: './document-workflow.component.html',
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
}
