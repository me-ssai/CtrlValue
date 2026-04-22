import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { ThemeService } from '../../services/theme.service';
import { IntegrationsComponent } from './integrations/integrations.component';
import { AuthService } from '../../services/auth.service';
import { EntityService, Entity, EntityUser, CreateEntityRequest } from '../../services/entity.service';
import { UpdateProfileRequest, ChangePasswordRequest } from '../../models/api.models';

@Component({
    selector: 'app-settings',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        FormsModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatButtonModule,
        MatSlideToggleModule,
        MatSelectModule,
        MatIconModule,
        MatSnackBarModule,
        MatDividerModule,
        MatProgressSpinnerModule,
        MatChipsModule,
        MatTooltipModule,
        IntegrationsComponent
    ],
    templateUrl: './settings.component.html',
    styleUrl: './settings.component.scss'
})
export class SettingsComponent implements OnInit {
    profileForm: FormGroup;
    passwordForm: FormGroup;

    selectedCurrency = 'AUD';
    currencies = ['AUD', 'USD', 'EUR', 'GBP', 'JPY', 'CNY', 'INR'];

    supportedCountries = [
        { code: 'AU', label: 'Australia (Basiq)' },
        { code: 'NZ', label: 'New Zealand (Basiq)' },
        { code: 'US', label: 'United States (Plaid)' },
        { code: 'CA', label: 'Canada (Plaid)' },
        { code: 'GB', label: 'United Kingdom (Plaid)' },
        { code: 'IE', label: 'Ireland (Plaid)' },
        { code: 'FR', label: 'France (Plaid)' },
        { code: 'ES', label: 'Spain (Plaid)' },
        { code: 'NL', label: 'Netherlands (Plaid)' },
        { code: 'DE', label: 'Germany (Plaid)' },
    ];

    profileLoading = false;
    passwordLoading = false;

    // ── Entities ──────────────────────────────────────────────────────────
    entities: Entity[] = [];
    entityUsers: Record<string, EntityUser[]> = {};
    expandedEntityId: string | null = null;
    editingEntityId: string | null = null;
    editingEntityName = '';
    editingEntityCurrency = '';
    editingEntityCountry = 'AU';
    showNewEntityForm = false;
    newEntityName = '';
    newEntityCurrency = 'AUD';
    newEntityCountry = 'AU';
    entitiesLoading = false;

    private readonly CURRENCY_KEY = 'ctrlvalue_currency';

    constructor(
        public themeService: ThemeService,
        public authService: AuthService,
        private entityService: EntityService,
        private fb: FormBuilder,
        private snackBar: MatSnackBar,
        private router: Router
    ) {
        const user = this.authService.currentUser;
        this.profileForm = this.fb.group({
            firstName: [user?.firstName || '', [Validators.required, Validators.maxLength(100)]],
            lastName: [user?.lastName || '', [Validators.required, Validators.maxLength(100)]]
        });

        this.passwordForm = this.fb.group({
            currentPassword: ['', Validators.required],
            newPassword: ['', [Validators.required, Validators.minLength(6)]],
            confirmPassword: ['', Validators.required]
        });
    }

    ngOnInit(): void {
        const savedCurrency = localStorage.getItem(this.CURRENCY_KEY);
        if (savedCurrency) this.selectedCurrency = savedCurrency;

        // Load entities
        this.loadEntities();
    }

    // ── Entities ──────────────────────────────────────────────────────────────

    loadEntities(): void {
        this.entitiesLoading = true;
        this.entityService.getEntities().subscribe({
            next: entities => { this.entities = entities; this.entitiesLoading = false; },
            error: () => { this.snackBar.open('Failed to load workspaces', 'Close', { duration: 3000 }); this.entitiesLoading = false; }
        });
    }

    toggleEntityUsers(entityId: string): void {
        if (this.expandedEntityId === entityId) {
            this.expandedEntityId = null;
            return;
        }
        this.expandedEntityId = entityId;
        if (!this.entityUsers[entityId]) {
            this.entityService.getEntityUsers(entityId).subscribe({
                next: users => this.entityUsers[entityId] = users,
                error: () => this.snackBar.open('Failed to load users', 'Close', { duration: 3000 })
            });
        }
    }

    startEditEntity(entity: Entity): void {
        this.editingEntityId = entity.id;
        this.editingEntityName = entity.name;
        this.editingEntityCurrency = entity.baseCurrency;
        this.editingEntityCountry = entity.country ?? 'AU';
    }

    cancelEditEntity(): void {
        this.editingEntityId = null;
    }

    saveEditEntity(entity: Entity): void {
        if (!this.editingEntityName.trim()) return;
        this.entityService.updateEntity(entity.id, {
            name: this.editingEntityName.trim(),
            baseCurrency: this.editingEntityCurrency,
            country: this.editingEntityCountry
        }).subscribe({
            next: updated => {
                const idx = this.entities.findIndex(e => e.id === entity.id);
                if (idx !== -1) this.entities[idx] = updated;
                this.editingEntityId = null;
                this.snackBar.open('Workspace updated', 'Close', { duration: 2500 });
            },
            error: () => this.snackBar.open('Failed to update workspace', 'Close', { duration: 3000 })
        });
    }

    createEntity(): void {
        if (!this.newEntityName.trim()) return;
        const req: CreateEntityRequest = { name: this.newEntityName.trim(), baseCurrency: this.newEntityCurrency, country: this.newEntityCountry };
        this.entityService.createEntity(req).subscribe({
            next: entity => {
                this.entities.push(entity);
                this.newEntityName = '';
                this.showNewEntityForm = false;
                this.snackBar.open(`Workspace "${entity.name}" created`, 'Close', { duration: 2500 });
            },
            error: () => this.snackBar.open('Failed to create workspace', 'Close', { duration: 3000 })
        });
    }

    removeUserFromEntity(entityId: string, userId: string, userEmail: string): void {
        if (!confirm(`Remove ${userEmail} from this workspace?`)) return;
        this.entityService.removeUserFromEntity(entityId, userId).subscribe({
            next: () => {
                if (this.entityUsers[entityId]) {
                    this.entityUsers[entityId] = this.entityUsers[entityId].filter(u => u.userId !== userId);
                }
                this.snackBar.open('User removed from workspace', 'Close', { duration: 2500 });
            },
            error: () => this.snackBar.open('Failed to remove user', 'Close', { duration: 3000 })
        });
    }

    toggleTheme(): void { this.themeService.toggleTheme(); }

    onCurrencyChange(currency: string): void {
        this.selectedCurrency = currency;
        localStorage.setItem(this.CURRENCY_KEY, currency);
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    updateProfile(): void {
        if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
        this.profileLoading = true;
        const request: UpdateProfileRequest = this.profileForm.value;
        this.authService.updateProfile(request).subscribe({
            next: () => {
                this.profileLoading = false;
                this.snackBar.open('Profile updated successfully', 'Close', { duration: 3000 });
            },
            error: (err) => {
                this.profileLoading = false;
                this.snackBar.open(err.error?.message || 'Failed to update profile', 'Close', { duration: 5000 });
            }
        });
    }

    // ── Password ──────────────────────────────────────────────────────────────

    changePassword(): void {
        if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
        const { newPassword, confirmPassword } = this.passwordForm.value;
        if (newPassword !== confirmPassword) {
            this.snackBar.open('Passwords do not match', 'Close', { duration: 3000 });
            return;
        }
        this.passwordLoading = true;
        const request: ChangePasswordRequest = {
            currentPassword: this.passwordForm.value.currentPassword,
            newPassword: this.passwordForm.value.newPassword
        };
        this.authService.changePassword(request).subscribe({
            next: () => {
                this.passwordLoading = false;
                this.snackBar.open('Password changed successfully', 'Close', { duration: 3000 });
                this.passwordForm.reset();
            },
            error: (err) => {
                this.passwordLoading = false;
                this.snackBar.open(err.error?.message || 'Failed to change password', 'Close', { duration: 5000 });
            }
        });
    }

    runSetupWizard(): void {
        this.router.navigate(['/onboarding']);
    }
}
