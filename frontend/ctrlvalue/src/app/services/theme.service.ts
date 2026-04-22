import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
    private readonly THEME_KEY = 'ctrlvalue_theme';
    private isDark = false;

    constructor() {
        this.loadTheme();
    }

    private loadTheme(): void {
        const savedTheme = localStorage.getItem(this.THEME_KEY);
        this.isDark = savedTheme === 'dark';
        this.applyTheme();
    }

    private applyTheme(): void {
        document.documentElement.setAttribute('data-theme', this.isDark ? 'dark' : 'light');
    }

    toggleTheme(): void {
        this.isDark = !this.isDark;
        localStorage.setItem(this.THEME_KEY, this.isDark ? 'dark' : 'light');
        this.applyTheme();
    }

    get isDarkMode(): boolean {
        return this.isDark;
    }
}
