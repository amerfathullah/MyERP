import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { LeadStore } from '../store/lead.store';
import { LeadService } from '../../proxy/crm/lead.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import type { LeadDto } from '../../proxy/crm/models';

@Component({
  selector: 'app-lead-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationModule, StatusBadgeComponent],
  templateUrl: './lead-detail.component.html',
  styleUrls: ['./lead-detail.component.scss'],
})
export class LeadDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private store = inject(LeadStore);
  private service = inject(LeadService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  lead: LeadDto | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((result) => {
      this.lead = result;
    });
  }

  qualify(): void {
    this.store.qualify(this.lead!.id!);
    this.reloadAfterAction();
  }

  markLost(): void {
    this.confirmation.warn('::CRM:MarkLeadLostConfirmation', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.markLost(this.lead!.id!);
        this.reloadAfterAction();
      }
    });
  }

  convertToOpportunity(): void {
    this.service.convertToOpportunity({
      leadId: this.lead!.id!,
      title: `${this.lead!.fullName ?? this.lead!.firstName} - Opportunity`,
      opportunityAmount: 0,
    }).subscribe({
      next: (opp) => {
        this.toaster.success('Lead converted to opportunity');
        this.router.navigate(['/crm/opportunities', opp.id]);
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? 'Conversion failed');
      },
    });
  }

  delete(): void {
    this.confirmation.warn('::DeleteConfirmationMessage', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.store.remove(this.lead!.id!);
        this.router.navigate(['/crm/leads']);
      }
    });
  }

  private reloadAfterAction(): void {
    setTimeout(() => {
      this.service.get(this.lead!.id!).subscribe((result) => {
        this.lead = result;
      });
    }, 500);
  }

  getStatusLabel(): string {
    const map: Record<number, string> = { 0: 'New', 1: 'Open', 2: 'Replied', 3: 'Interested', 4: 'Qualified', 5: 'Converted', 6: 'Lost', 7: 'DoNotContact' };
    return map[this.lead?.status ?? 0] ?? 'New';
  }

  getSourceLabel(): string {
    const map: Record<number, string> = { 0: 'Website', 1: 'Referral', 2: 'Campaign', 3: 'Cold Call', 4: 'Advertisement', 5: 'Social Media', 6: 'Trade Show', 7: 'Partner', 8: 'Other' };
    return map[this.lead?.source ?? 0] ?? 'Other';
  }
}
