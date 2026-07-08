import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-delivery-note-list',
  standalone: true,
  imports: [CommonModule, PageModule, MatCardModule, MatTableModule, MatButtonModule, MatIconModule, StatusBadgeComponent],
  templateUrl: './delivery-note-list.component.html',
  styleUrls: ['./delivery-note-list.component.scss'],
})
export class DeliveryNoteListComponent {
  private router = inject(Router);
  deliveryNotes: any[] = [];
  displayedColumns = ['dnNumber', 'date', 'customerName', 'salesOrder', 'status'];

  create(): void { /* TODO */ }
}
