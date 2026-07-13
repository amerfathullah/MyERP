import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { IssueDetailService, type IssueDetailDto } from '../../proxy/detail-services';

@Component({
  selector: 'app-issue-detail', standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'Issues' | abpLocalization">
      @if (d) {
        <div class="card"><div class="card-body">
          <h5>{{ d.subject }}</h5>
          <div class="row mt-3">
            <div class="col-md-3"><strong>{{ 'Priority' | abpLocalization }}:</strong> {{ d.priority }}</div>
            <div class="col-md-3"><strong>{{ 'Status' | abpLocalization }}:</strong> <span class="badge bg-info">{{ ['Open','Replied','On Hold','Closed','Cancelled'][d.status] }}</span></div>
            <div class="col-md-3"><strong>{{ 'Type' | abpLocalization }}:</strong> {{ d.issueType ?? '—' }}</div>
            <div class="col-md-3"><strong>{{ 'Date' | abpLocalization }}:</strong> {{ d.creationTime | date:'dd/MM/yyyy' }}</div>
          </div>
          @if (d.description) { <div class="mt-3"><strong>{{ 'Description' | abpLocalization }}:</strong><p class="mt-1">{{ d.description }}</p></div> }
          <div class="mt-3 d-flex gap-2">
            @if (d.status === 0) { <button class="btn btn-sm btn-success" (click)="action('reply')"><i class="fa fa-reply me-1"></i>Reply</button> }
            @if (d.status < 3) { <button class="btn btn-sm btn-primary" (click)="action('resolve')"><i class="fa fa-check me-1"></i>Resolve</button> }
          </div>
        </div></div>
      }
    </abp-page>
  `,
})
export class IssueDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(IssueDetailService);
  d: IssueDetailDto | null = null;
  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }
  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    if (type === 'reply') this.service.reply(id).subscribe(() => this.ngOnInit());
    else if (type === 'resolve') this.service.resolve(id).subscribe(() => this.ngOnInit());
  }
}
