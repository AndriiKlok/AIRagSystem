import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/areas',
    pathMatch: 'full'
  },
  {
    path: 'areas',
    loadComponent: () => import('./features/areas/area-list/area-list').then(m => m.AreaList)
  },
  {
    path: 'areas/:id',
    loadComponent: () => import('./features/areas/area-detail/area-detail').then(m => m.AreaDetail)
  },
  {
    path: 'documents/upload',
    loadComponent: () => import('./features/documents/document-upload/document-upload').then(m => m.DocumentUpload)
  },
  {
    path: 'chats/:id',
    loadComponent: () => import('./features/chats/chat-window/chat-window').then(m => m.ChatWindow)
  }
];
