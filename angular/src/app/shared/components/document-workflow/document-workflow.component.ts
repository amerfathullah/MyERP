import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent, DocumentStatus } from '../status-badge/status-badge.component';

export interface WorkflowAction {
  name: string;
  label: string;
  icon: string;
  color: 'primary' | 'accent' | 'warn' | '';
}

@Component({
  selector: 'app-document-workflow',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, StatusBadgeComponent],
  templateUrl: './document-workflow.component.html',
  styleUrls: ['./document-workflow.component.scss'],
})
export class DocumentWorkflowComponent {
  @Input({ required: true }) currentStatus!: DocumentStatus;
  @Input() availableActions: WorkflowAction[] = [];
  @Output() actionTriggered = new EventEmitter<string>();
}
