import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { roleGuard } from './guards/role.guard';
import { demoBlockedGuard } from './guards/demo.guard';

export const routes: Routes = [
    {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
    },
    {
        path: 'login',
        loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent)
    },
    {
        path: 'register',
        loadComponent: () => import('./pages/register/register.component').then(m => m.RegisterComponent)
    },
    {
        path: 'verify-email',
        loadComponent: () => import('./pages/verify-email/verify-email.component').then(m => m.VerifyEmailComponent)
    },
    {
        path: 'onboarding',
        loadComponent: () => import('./pages/onboarding/onboarding.component').then(m => m.OnboardingComponent),
        canActivate: [authGuard]
    },
    // ── Super Admin (SuperAdmin role only) ──
    {
        path: 'super-admin',
        loadComponent: () => import('./pages/super-admin/super-admin.component').then(m => m.SuperAdminComponent),
        canActivate: [demoBlockedGuard, authGuard, roleGuard('SuperAdmin')]
    },
    // ── Site Admin (SiteAdmin or SuperAdmin) ──
    {
        path: 'site-admin',
        loadComponent: () => import('./pages/site-admin/site-admin.component').then(m => m.SiteAdminComponent),
        canActivate: [demoBlockedGuard, authGuard, roleGuard('SiteAdmin')]
    },
    // Main layout wrapper for all authenticated pages
    {
        path: '',
        loadComponent: () => import('./layouts/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
        canActivate: [authGuard],
        children: [
            {
                path: 'dashboard',
                loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            // Accounts routes
            {
                path: 'accounts',
                loadComponent: () => import('./pages/accounts/accounts.component').then(m => m.AccountsComponent)
            },
            {
                path: 'accounts/:id',
                loadComponent: () => import('./pages/accounts/account-detail/account-detail.component').then(m => m.AccountDetailComponent)
            },
            // Transactions routes (retired — access via account detail)
            // {
            //     path: 'transactions',
            //     loadComponent: () => import('./pages/transactions/transactions.component').then(m => m.TransactionsComponent)
            // },
            // Categories routes
            {
                path: 'categories',
                loadComponent: () => import('./pages/categories/categories.component').then(m => m.CategoriesComponent)
            },
            // ── Investing sections ──
            {
                path: 'investing',
                loadComponent: () => import('./pages/investing/hub/investment-hub.component').then(m => m.InvestmentHubComponent)
            },
            {
                path: 'investing/stocks',
                loadComponent: () => import('./pages/investing/stocks/stocks.component').then(m => m.StocksComponent)
            },
            {
                path: 'investing/etfs',
                loadComponent: () => import('./pages/investing/etfs/etfs.component').then(m => m.EtfsComponent)
            },
            {
                path: 'investing/bonds',
                loadComponent: () => import('./pages/investing/bonds/bonds.component').then(m => m.BondsComponent)
            },
            {
                path: 'investing/metals',
                loadComponent: () => import('./pages/investing/metals/metals.component').then(m => m.MetalsComponent)
            },
            {
                path: 'investing/crypto',
                loadComponent: () => import('./pages/investing/crypto/crypto.component').then(m => m.CryptoComponent)
            },
            {
                path: 'investing/real-estate',
                loadComponent: () => import('./pages/investing/real-estate/real-estate.component').then(m => m.RealEstateComponent)
            },
            {
                path: 'investing/super',
                loadComponent: () => import('./pages/investing/super/super.component').then(m => m.SuperComponent)
            },
            {
                path: 'investing/other',
                loadComponent: () => import('./pages/investing/other/other-investments.component').then(m => m.OtherInvestmentsComponent)
            },
            // Legacy redirects — keep old bookmarks working
            { path: 'instruments',   redirectTo: 'investing/stocks', pathMatch: 'full' },
            { path: 'positions',     redirectTo: 'investing/stocks', pathMatch: 'full' },
            { path: 'price-history', redirectTo: 'investing/stocks', pathMatch: 'full' },
            {
                path: 'valuations',
                loadComponent: () => import('./pages/valuations/valuations.component').then(m => m.ValuationsComponent)
            },
            {
                path: 'depreciation-schedules',
                loadComponent: () => import('./pages/depreciation-schedules/depreciation-schedules.component').then(m => m.DepreciationSchedulesComponent)
            },
            // Budgets
            {
                path: 'budgets',
                loadComponent: () => import('./pages/budgets/budgets.component').then(m => m.BudgetsComponent)
            },
            // ── Intelligence ──
            {
                path: 'intelligence/agent',
                loadComponent: () => import('./pages/intelligence/agent/agent.component').then(m => m.AgentComponent)
            },
            // Settings route (blocked in demo mode)
            {
                path: 'settings',
                loadComponent: () => import('./pages/settings/settings.component').then(m => m.SettingsComponent),
                canActivate: [demoBlockedGuard]
            },
            // Assets & Liabilities
            {
                path: 'assets',
                loadComponent: () => import('./pages/assets/assets.component').then(m => m.AssetsComponent)
            },
            {
                path: 'liabilities',
                loadComponent: () => import('./pages/liabilities/liabilities.component').then(m => m.LiabilitiesComponent)
            }
        ]
    },
    {
        path: '**',
        redirectTo: 'dashboard'
    }
];

