import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';
import type { QualityInspectionDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-quality-inspection-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  templateUrl: './quality-inspection-list.component.html',
})
export class QualityInspectionListComponent implements OnInit {
  private service = inject(QualityInspectionService);
  inspections: QualityInspectionDto[] = [];
  totalCount = 0;
  isLoading = false;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 20 }).subscribe({
      next: (result) => {
        this.inspections = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Accepted', 'Rejected'][status ?? 0] ?? 'Draft';
  }

  getTypeLabel(type: number | undefined): string {
    return ['Incoming', 'Outgoing', 'In Process'][type ?? 0] ?? 'Incoming';
  }
}
