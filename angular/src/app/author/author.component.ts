import { FormGroup, FormBuilder, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Component, inject, OnInit } from '@angular/core';
import { DatePipe, formatDate } from '@angular/common';
import { NgbDatepickerModule, NgbDateStruct, NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { ListService, PagedResultDto, LocalizationPipe, PermissionDirective, AutofocusDirective } from '@abp/ng.core';
import { ConfirmationService, Confirmation, NgxDatatableDefaultDirective, NgxDatatableListDirective, ModalCloseDirective, ModalComponent } from '@abp/ng.theme.shared';
import { AuthorService, AuthorDto } from '../proxy/authors';
@Component({ selector: 'app-author', templateUrl: './author.component.html', imports: [FormsModule, ReactiveFormsModule, NgbDatepickerModule, NgxDatatableModule, NgbDropdownModule, ModalComponent, AutofocusDirective, NgxDatatableListDirective, NgxDatatableDefaultDirective, PermissionDirective, ModalCloseDirective, LocalizationPipe, DatePipe], providers: [ListService] })
export class AuthorComponent implements OnInit {
  public readonly list = inject(ListService);
  private authorService = inject(AuthorService);
  private fb = inject(FormBuilder);
  private confirmation = inject(ConfirmationService);
  author = { items: [], totalCount: 0 } as PagedResultDto<AuthorDto>;
  selectedAuthor = {} as AuthorDto;
  form: FormGroup;
  isModalOpen = false;
  ngOnInit() { const authorStreamCreator = query => this.authorService.getList(query); this.list.hookToQuery(authorStreamCreator).subscribe(response => { this.author = response; }); }
  createAuthor() { this.selectedAuthor = {} as AuthorDto; this.buildForm(); this.isModalOpen = true; }
  editAuthor(id: string) { this.authorService.get(id).subscribe(author => { this.selectedAuthor = author; this.buildForm(); this.isModalOpen = true; }); }
  delete(id: string) { this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe(status => { if (status === Confirmation.Status.confirm) { this.authorService.delete(id).subscribe(() => this.list.get()); } }); }
  exportToExcel() { this.authorService.getDownloadToken().subscribe(result => { const token = encodeURIComponent(result.token || ''); window.location.href = `/api/app/author/as-excel-file?downloadToken=${token}`; }); }
  buildForm() { this.form = this.fb.group({ name: [this.selectedAuthor.name || '', Validators.required], birthDate: [this.selectedAuthor.birthDate ? this.parseDate(this.selectedAuthor.birthDate) : null, Validators.required], shortBio: [this.selectedAuthor.shortBio || ''] }); }
  save() { if (this.form.invalid) { return; } const formValue = this.form.value; const requestData = { ...formValue, birthDate: this.formatDate(formValue.birthDate) }; let request = this.authorService.create(requestData); if (this.selectedAuthor.id) { request = this.authorService.update(this.selectedAuthor.id, requestData); } request.subscribe(() => { this.isModalOpen = false; this.form.reset(); this.list.get(); }); }
  private parseDate(value: string | Date): NgbDateStruct | null { if (!value) { return null; } const date = new Date(value); if (isNaN(date.getTime())) { return null; } return { year: date.getFullYear(), month: date.getMonth() + 1, day: date.getDate() }; }
  private formatDate(dateStruct: NgbDateStruct | null): string { if (!dateStruct) { return ''; } const date = new Date(dateStruct.year, dateStruct.month - 1, dateStruct.day); return formatDate(date, 'yyyy-MM-dd', 'en'); }
}
