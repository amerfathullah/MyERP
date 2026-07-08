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
  @Input({ required: true }) currentStatus!: string;
  @Input() availableActions: WorkflowAction[] = [];
  @Output() actionTriggered = new EventEmitter<string>();
}
