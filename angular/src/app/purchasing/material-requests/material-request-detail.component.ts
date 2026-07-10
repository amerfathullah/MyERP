import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { MaterialRequestStore } from '../store/material-request.store';
import { MaterialRequestService } from '../../proxy/purchasing/material-request.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import type { MaterialRequestDto } from '../../proxy/purchasing/models';

@Component({
  selector: 'app-material-request-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, StatusBadgeComponent, LoadingOverlayComponent],
  templateUrl: './material-request-detail.component.html',
  styleUrls: ['./material-request-detail.component.scss'],
})
export class MaterialRequestDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  readonly store = inject(MaterialRequestStore);
  private service = inject(MaterialRequestService);
  private confirmation = inject(ConfirmationService);

  entity: MaterialRequestDto | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.entity = result;
    });
  }

  getTypeLabel(type: number | undefined): string {
    return ['Purchase', 'Material Transfer', 'Material Issue', 'Manufacture'][type ?? 0] ?? 'Purchase';
  }

  getStatusLabel(status: number | undefined): string {
    return ['Draft', 'Submitted', 'Approved', 'Posted', 'Cancelled', 'Rejected'][status ?? 0] ?? 'Draft';
  }

  submit(): void {
    this.store.submitRequest(this.entity!.id!);
    this.reloadAfterAction();
  }

  cancel(): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.cancelRequest(this.entity!.id!);
        this.reloadAfterAction();
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/purchasing/material-requests']);
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.entity!.id!).subscribe((result) => {
        this.entity = result;
      });
    }, 500);
  }
}
