import { Component, signal, OnInit, Inject, PLATFORM_ID } from '@angular/core';
import { RouterOutlet, RouterModule } from '@angular/router';
import { isPlatformBrowser } from '@angular/common';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  protected readonly title = signal('company-knowledge-assistant-ui');

  constructor(@Inject(PLATFORM_ID) private platformId: Object) {}

  ngOnInit(): void {
    // Initialize theme only in browser environment
    if (isPlatformBrowser(this.platformId)) {
      this.initializeTheme();
    }
  }

  private initializeTheme(): void {
    // Check for saved theme preference or default to dark
    const savedTheme = localStorage.getItem('theme') || 'dark';
    this.setTheme(savedTheme);

    // Add theme toggle listener after view is initialized
    setTimeout(() => {
      const toggleButton = document.getElementById('themeToggle');
      if (toggleButton) {
        toggleButton.addEventListener('click', () => this.toggleTheme());
      }
    });
  }

  private setTheme(theme: string): void {
    const body = document.body;
    const icon = document.getElementById('themeIcon');

    if (theme === 'dark') {
      body.classList.remove('light-theme');
      if (icon) {
        icon.className = 'bi bi-sun-fill';
      }
    } else {
      body.classList.add('light-theme');
      if (icon) {
        icon.className = 'bi bi-moon-fill';
      }
    }

    localStorage.setItem('theme', theme);
  }

  private toggleTheme(): void {
    const currentTheme = localStorage.getItem('theme') || 'dark';
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    this.setTheme(newTheme);
  }
}
