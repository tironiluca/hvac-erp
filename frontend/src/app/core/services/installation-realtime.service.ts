import { Injectable, computed, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { TenantSelectionService } from './tenant-selection.service';
import { CalendarPresenceUser, ScheduledInstallation } from '../models/installation.model';

/**
 * SignalR client for the shared real-time installation calendar. The server pushes every
 * `ScheduledInstallation` change (create / reschedule / delete) to the users of the same store, so one
 * person moving an appointment updates everyone else's grid without a refresh. Mirrors the
 * `IInstallationCalendarClient` hub contract on the API side.
 *
 * The connection is optional — if the hub is unreachable the calendar still works fully over
 * REST, it just won't live-sync.
 */
@Injectable({ providedIn: 'root' })
export class InstallationRealtimeService {
  private auth = inject(AuthService);
  private selection = inject(TenantSelectionService);

  private connection?: HubConnection;

  private readonly changedSubject = new Subject<ScheduledInstallation>();
  private readonly deletedSubject = new Subject<number>();

  /** An appointment was created or moved (by anyone — includes the echo of this client's own edits). */
  readonly changed$ = this.changedSubject.asObservable();
  /** An appointment was removed. */
  readonly deleted$ = this.deletedSubject.asObservable();

  private readonly presenceSignal = signal<CalendarPresenceUser[]>([]);
  /** Everyone currently on the calendar page, this client included. Empty while disconnected. */
  readonly presence = this.presenceSignal.asReadonly();

  private readonly state = signal<HubConnectionState>(HubConnectionState.Disconnected);
  readonly connected = computed(() => this.state() === HubConnectionState.Connected);

  /** Opens the hub connection. Idempotent — a second call while connected is a no-op. */
  async connect(): Promise<void> {
    if (this.connection) {
      return;
    }

    // environment.apiUrl is e.g. https://host/api — the hub is mapped at the site root.
    // A browser WebSocket cannot carry headers, so the tenant / store selection travels in the
    // query string; the hub then pushes only that tenant's and those stores' changes.
    const query = new URLSearchParams();
    const tenantId = this.selection.tenantId();
    const storeId = this.selection.storeId();
    if (tenantId !== null) query.set('tenant', String(tenantId));
    if (storeId !== null) query.set('store', String(storeId));
    const suffix = query.size ? `?${query}` : '';
    const hubUrl = `${environment.apiUrl.replace(/\/api\/?$/, '')}/hubs/installations${suffix}`;
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.auth.currentSession()?.access_token ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('InstallationChanged', (i: ScheduledInstallation) => this.changedSubject.next(i));
    connection.on('InstallationDeleted', (id: number) => this.deletedSubject.next(id));
    connection.on('PresenceChanged', (viewers: CalendarPresenceUser[]) =>
      this.presenceSignal.set(viewers ?? []),
    );

    connection.onreconnecting(() => this.state.set(HubConnectionState.Reconnecting));
    connection.onreconnected(() => this.state.set(HubConnectionState.Connected));
    connection.onclose(() => {
      this.state.set(HubConnectionState.Disconnected);
      this.presenceSignal.set([]);
    });

    this.connection = connection;

    try {
      await connection.start();
      this.state.set(HubConnectionState.Connected);
    } catch {
      this.connection = undefined;
      this.state.set(HubConnectionState.Disconnected);
    }
  }

  /** Stops the SignalR connection and clears the presence roster. */
  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = undefined;
    this.state.set(HubConnectionState.Disconnected);
    this.presenceSignal.set([]);
    await connection?.stop().catch(() => undefined);
  }
}
