import { Directive, ElementRef, inject, OnInit, OnDestroy, Renderer2 } from '@angular/core';
import { NgControl } from '@angular/forms';
import { Subscription } from 'rxjs';

/**
 * Auto-validation directive that shows Bootstrap invalid-feedback text
 * when a form control has validation errors and is touched.
 *
 * Usage: Add `appAutoValidation` to any form control with `formControlName`:
 *   <input formControlName="name" appAutoValidation />
 *
 * Or apply globally via CSS by adding invalid-feedback after is-invalid inputs.
 */
@Directive({
  selector: '[appAutoValidation],[formControlName]',
  standalone: true,
})
export class AutoValidationDirective implements OnInit, OnDestroy {
  private el = inject(ElementRef);
  private renderer = inject(Renderer2);
  private ngControl = inject(NgControl, { optional: true });
  private sub?: Subscription;
  private feedbackEl: HTMLElement | null = null;

  ngOnInit(): void {
    if (!this.ngControl?.control) return;

    this.sub = this.ngControl.control.statusChanges?.subscribe(() => {
      this.updateValidation();
    });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private updateValidation(): void {
    const control = this.ngControl?.control;
    if (!control) return;

    const el = this.el.nativeElement as HTMLElement;

    if (control.invalid && control.touched) {
      this.renderer.addClass(el, 'is-invalid');
      this.showFeedback(control.errors);
    } else {
      this.renderer.removeClass(el, 'is-invalid');
      this.removeFeedback();
    }
  }

  private showFeedback(errors: any): void {
    if (!errors) return;
    const message = this.getErrorMessage(errors);
    if (!message) return;

    if (!this.feedbackEl) {
      this.feedbackEl = this.renderer.createElement('div');
      this.renderer.addClass(this.feedbackEl, 'invalid-feedback');
      this.renderer.appendChild(this.el.nativeElement.parentNode, this.feedbackEl);
    }
    this.feedbackEl!.textContent = message;
  }

  private removeFeedback(): void {
    if (this.feedbackEl) {
      this.feedbackEl.remove();
      this.feedbackEl = null;
    }
  }

  private getErrorMessage(errors: any): string {
    if (errors['required']) return 'This field is required';
    if (errors['email']) return 'Invalid email address';
    if (errors['min']) return `Minimum value is ${errors['min'].min}`;
    if (errors['max']) return `Maximum value is ${errors['max'].max}`;
    if (errors['minlength']) return `Minimum ${errors['minlength'].requiredLength} characters`;
    if (errors['maxlength']) return `Maximum ${errors['maxlength'].requiredLength} characters`;
    if (errors['pattern']) return 'Invalid format';
    return 'Invalid value';
  }
}
