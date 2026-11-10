import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoModule } from '@jsverse/transloco';
import { AuthService } from '../../services/auth.service';
import { CurrentUserService } from '../../services/current-user.service';

/**
 * Shown to someone who signed in but belongs to no company yet. Signing in grants nothing by
 * itself: an administrator has to add the account to a company first. Nothing else in the app
 * would work for them — every API call is refused.
 */
@Component({
  selector: 'hvac-no-access',
  imports: [MatButtonModule, MatIconModule, TranslocoModule],
  templateUrl: './no-access.component.html',
  styleUrl: './no-access.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NoAccessComponent {
  protected auth = inject(AuthService);
  private currentUser = inject(CurrentUserService);
  private document = inject(DOCUMENT);

  /** Asks again: an administrator may have granted access in the meantime. */
  retry(): void {
    this.document.location.assign('/');
  }

  async signOut(): Promise<void> {
    this.currentUser.clear();
    await this.auth.signOut();
    this.document.location.assign('/auth/login');
  }
}
