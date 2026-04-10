import { Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { DocumentManagerComponent } from './features/document-manager/document-manager.component';
import { ChatComponent } from './features/chat/chat.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MatTabsModule, MatIconModule, DocumentManagerComponent, ChatComponent],
  template: `
    <mat-tab-group class="app-tabs" animationDuration="200ms">
      <mat-tab>
        <ng-template mat-tab-label>
          <mat-icon>description</mat-icon>
          &nbsp;Documents
        </ng-template>
        <app-document-manager />
      </mat-tab>
      <mat-tab>
        <ng-template mat-tab-label>
          <mat-icon>chat</mat-icon>
          &nbsp;Chat
        </ng-template>
        <app-chat />
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [`
    .app-tabs {
      height: 100vh;
      display: flex;
      flex-direction: column;
    }
    :host ::ng-deep .mat-mdc-tab-body-wrapper {
      flex: 1;
      overflow: hidden;
    }
    :host ::ng-deep .mat-mdc-tab-body-content {
      height: 100%;
      overflow: hidden;
    }
  `]
})
export class AppComponent {}
