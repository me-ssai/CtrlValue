import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatMenuModule } from '@angular/material/menu';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';
import { DemoStateService } from '../../services/demo-state.service';
import { DemoBannerComponent } from '../../shared/demo-banner/demo-banner.component';
import { EntitySelectorComponent } from '../../shared/entity-selector/entity-selector.component';
import { TickerStripComponent } from '../../shared/ticker-strip/ticker-strip.component';

@Component({
    selector: 'app-main-layout',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatSidenavModule,
        MatToolbarModule,
        MatListModule,
        MatIconModule,
        MatButtonModule,
        MatDividerModule,
        MatTooltipModule,
        MatExpansionModule,
        MatMenuModule,
        EntitySelectorComponent,
        TickerStripComponent,
        DemoBannerComponent,
    ],
    templateUrl: './main-layout.component.html',
    styleUrl: './main-layout.component.scss'
})
export class MainLayoutComponent {
    authService = inject(AuthService);
    demoState = inject(DemoStateService);
    private router = inject(Router);

    sidebarCollapsed = true;
    readonly isDemoMode = environment.demo;

    toggleSidebar(): void {
        this.sidebarCollapsed = !this.sidebarCollapsed;
    }

    logout(): void {
        this.authService.logout();
        this.router.navigate(['/login']);
    }

    navigateTo(path: string): void {
        this.router.navigate([path]);
    }
}
