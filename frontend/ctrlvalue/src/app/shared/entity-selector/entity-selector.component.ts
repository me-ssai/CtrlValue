import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { EntityService, Entity } from '../../services/entity.service';
import { MatDividerModule } from '@angular/material/divider';
import { AuthService } from '../../services/auth.service';


@Component({
  selector: 'app-entity-selector',
  standalone: true,
  imports: [
    CommonModule,
    MatSelectModule,
    MatFormFieldModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatDividerModule
  ],
  templateUrl: './entity-selector.component.html',
  styleUrl: './entity-selector.component.scss'
})
export class EntitySelectorComponent implements OnInit {
  private entityService = inject(EntityService);
  private router = inject(Router);
  private authService = inject(AuthService);

  entities: Entity[] = [];
  currentEntity: Entity | null = null;
  loading = false;

  ngOnInit(): void {
    // Subscribe to current entity
    this.entityService.currentEntity$.subscribe(entity => {
      this.currentEntity = entity;
    });

    // Load all entities
    this.loadEntities();
  }

  loadEntities(): void {
    this.loading = true;
    this.entityService.getEntities().subscribe({
      next: (entities) => {
        this.entities = entities;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading entities:', error);
        this.loading = false;
      }
    });
  }

  canAccessSiteAdmin(): boolean {
    const currentUserRole = this.authService.getUserRole();
    return currentUserRole == "SiteAdmin" || currentUserRole == "SuperAdmin";
    // or this.authService.hasRole('SiteAdmin');
  }

  onEntityChange(entity: Entity): void {
    this.entityService.setCurrentEntity(entity);
    // Reload current page to refresh data
    window.location.reload();
  }

  getUserRole(entity: Entity): string {
    const currentUser = entity.users?.find(u => u.userId === this.currentEntity?.id);
    return currentUser?.role || 'VIEWER';
  }

  getRoleIcon(role: string): string {
    switch (role) {
      case 'OWNER': return 'admin_panel_settings';
      case 'EDITOR': return 'edit';
      case 'VIEWER': return 'visibility';
      default: return 'person';
    }
  }

  manageEntities(): void {
    this.router.navigate(['/site-admin']);
  }
}
