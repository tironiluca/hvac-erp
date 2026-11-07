import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/services/language.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'hvac-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class App {
  // Injected here (not just in ShellComponent) so the stored theme / language apply from
  // first paint, including on the login page, which sits outside the shell/authGuard.
  private theme = inject(ThemeService);
  private language = inject(LanguageService);
}
