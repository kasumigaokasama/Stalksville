import { Routes } from '@angular/router';

import { adminGuard, authGuard } from './core/auth/auth-guard';
import { Shell } from './core/layout/shell';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.Login),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
        title: 'Stalksville — Overview',
      },
      {
        path: 'players',
        loadComponent: () => import('./features/players/players').then((m) => m.Players),
        title: 'Stalksville — Players',
      },
      {
        path: 'highscores',
        loadComponent: () => import('./features/highscores/highscores').then((m) => m.Highscores),
        title: 'Stalksville — Highscores',
      },
      {
        path: 'ranked',
        loadComponent: () => import('./features/ranked/ranked').then((m) => m.Ranked),
        title: 'Stalksville — Ranked',
      },
      {
        // Static segment must precede players/:id, otherwise 'compare' is treated as an id.
        path: 'players/compare',
        loadComponent: () => import('./features/players/player-compare').then((m) => m.PlayerCompare),
        title: 'Stalksville — Compare players',
      },
      {
        path: 'players/:id',
        loadComponent: () => import('./features/players/player-dossier').then((m) => m.PlayerDossier),
        title: 'Stalksville — Player dossier',
      },
      {
        path: 'clans',
        loadComponent: () => import('./features/clans/clans').then((m) => m.Clans),
        title: 'Stalksville — Clans',
      },
      {
        path: 'clans/:id',
        loadComponent: () => import('./features/clans/clan-detail').then((m) => m.ClanDetail),
        title: 'Stalksville — Clan',
      },
      {
        path: 'investigations',
        loadComponent: () => import('./features/investigations/investigations').then((m) => m.Investigations),
        title: 'Stalksville — Investigations',
      },
      {
        path: 'investigations/:id',
        loadComponent: () => import('./features/investigations/investigation-workspace').then((m) => m.InvestigationWorkspace),
        title: 'Stalksville — Investigation',
      },
      {
        path: 'timeline',
        loadComponent: () => import('./features/timeline/timeline').then((m) => m.Timeline),
        title: 'Stalksville — Timeline',
      },
      {
        path: 'alerts',
        loadComponent: () => import('./features/alerts/alerts').then((m) => m.Alerts),
        title: 'Stalksville — Alerts',
      },
      {
        path: 'graph',
        loadComponent: () => import('./features/graph/graph').then((m) => m.Graph),
        title: 'Stalksville — Graph',
      },
      {
        path: 'analytics',
        loadComponent: () => import('./features/analytics/analytics').then((m) => m.Analytics),
        title: 'Stalksville — Analytics',
      },
      {
        path: 'scans',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/scans/scans').then((m) => m.Scans),
        title: 'Stalksville — Scheduled scans',
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings').then((m) => m.Settings),
        title: 'Stalksville — Settings',
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin').then((m) => m.Admin),
        title: 'Stalksville — Admin',
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
