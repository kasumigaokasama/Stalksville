import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';

interface EraseDialogData {
  username: string;
}

/**
 * Data-protection erasure confirmation: a typed reason (for the audit log) is mandatory before
 * the destructive delete is issued. Closing without a reason cancels the erasure.
 */
@Component({
  selector: 'stl-erase-confirm',
  styleUrl: './erase-confirm.scss',
  templateUrl: './erase-confirm.html',
})
export class EraseConfirm {
  private readonly dialogRef = inject(DialogRef<string>);

  protected readonly data = inject<EraseDialogData>(DIALOG_DATA);

  protected readonly reason = signal('');
  protected readonly tooShort = signal(false);

  protected confirm(): void {
    const reason = this.reason().trim();
    if (reason.length < 4) {
      this.tooShort.set(true);
      return;
    }
    this.dialogRef.close(reason);
  }

  protected cancel(): void {
    this.dialogRef.close();
  }
}
