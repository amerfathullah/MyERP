import { Component, EventEmitter, forwardRef, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Subject, debounceTime, Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

/**
 * Generic Link-field search-select, generalized from ItemPickerComponent so any entity with a
 * server-side `filter` param (Customer, Supplier, ...) can get the same debounced type-ahead
 * instead of a plain <select> preloaded with a 200-500 record cap.
 *
 * Plugs into reactive forms via formControlName (implements ControlValueAccessor). Consumers
 * supply the search/get functions and a display-label function — this component has no
 * knowledge of the entity shape.
 */
@Component({
  selector: 'app-link-picker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => LinkPickerComponent),
      multi: true,
    },
  ],
  template: `
    <div class="position-relative">
      <input
        type="text"
        class="form-control"
        [class.is-invalid]="invalid"
        [placeholder]="placeholder"
        [disabled]="disabled"
        [(ngModel)]="searchText"
        (ngModelChange)="onSearchChange($event)"
        (focus)="onFocus()"
        (blur)="onBlur()"
      />
      @if (showDropdown() && results().length > 0) {
        <div class="list-group position-absolute w-100 shadow-sm" style="z-index:1050; max-height:260px; overflow-y:auto;">
          @for (item of results(); track displayFn(item)) {
            <button type="button" class="list-group-item list-group-item-action py-1 px-2"
              (mousedown)="selectItem(item)">
              {{ displayFn(item) }}
            </button>
          }
        </div>
      }
    </div>
  `,
})
export class LinkPickerComponent implements ControlValueAccessor {
  /** (filter: string) => Observable<items[]> — server-side search, expected debounced by the caller's service. */
  @Input({ required: true }) searchFn!: (filter: string) => Observable<any[]>;
  /** (id: string) => Observable<item> — resolves the display label when a value is written in (edit mode). */
  @Input({ required: true }) getByIdFn!: (id: string) => Observable<any>;
  /** item => string shown in the input/dropdown. */
  @Input({ required: true }) displayFn!: (item: any) => string;
  @Input() idFn: (item: any) => string = (item) => item?.id;
  @Input() placeholder = 'Search...';
  @Input() invalid = false;

  /** Emits the full selected item (or null when cleared). */
  @Output() linkSelected = new EventEmitter<any | null>();

  searchText = '';
  results = signal<any[]>([]);
  showDropdown = signal(false);
  disabled = false;

  private valueId: string | null = null;
  private searchSubject = new Subject<string>();
  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    this.searchSubject.pipe(debounceTime(300)).subscribe((query) => {
      if (!query || query.length < 1) {
        this.results.set([]);
        return;
      }
      this.searchFn(query)
        .pipe(catchError(() => of([])))
        .subscribe((items) => this.results.set(items ?? []));
    });
  }

  onSearchChange(query: string): void {
    this.showDropdown.set(true);
    this.searchSubject.next(query);
    if (!query) {
      this.valueId = null;
      this.onChange(null);
      this.linkSelected.emit(null);
    }
  }

  onFocus(): void {
    if (this.searchText) this.showDropdown.set(true);
  }

  onBlur(): void {
    // Delay so the (mousedown) selection on a dropdown item fires before the dropdown closes.
    setTimeout(() => this.showDropdown.set(false), 150);
    this.onTouched();
  }

  selectItem(item: any): void {
    this.valueId = this.idFn(item) ?? null;
    this.searchText = this.displayFn(item);
    this.showDropdown.set(false);
    this.onChange(this.valueId);
    this.linkSelected.emit(item);
  }

  writeValue(value: string | null): void {
    this.valueId = value;
    if (!value) {
      this.searchText = '';
      return;
    }
    this.getByIdFn(value)
      .pipe(catchError(() => of(null)))
      .subscribe((item) => {
        this.searchText = item ? this.displayFn(item) : '';
      });
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }
}
